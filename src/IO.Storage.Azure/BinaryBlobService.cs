using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.Azure.Utilities;
using Regira.IO.Storage.Utilities;
using Regira.IO.Utilities;

namespace Regira.IO.Storage.Azure;

public class BinaryBlobService(AzureCommunicator communicator) : IFileService
{
    protected internal BlobContainerClient Container
    {
        get
        {
            if (communicator.Container == null)
            {
                throw new Exception("ContainerClient is not opened yet!");
            }

            return communicator.Container!;
        }
    }
    public string Root => Container.Uri.ToString();


    public async Task<bool> Exists(string identifier)
    {
        await communicator.Open();
        var blob = GetBlobReference(identifier);
        return await blob.ExistsAsync();
    }
    public async Task<byte[]?> GetBytes(string identifier)
    {
        var response = await Download(identifier);
        return response?.Value.Content.ToArray();
    }
    public async Task<Stream?> GetStream(string identifier)
    {
        var response = await Download(identifier);
        return response?.Value.Content.ToStream();
    }
    public async Task<IEnumerable<string>> List(FileSearchObject? so = null)
    {
        so ??= new FileSearchObject();

        await communicator.Open();
        var identifiers = new List<string>();
        await foreach (var blob in ListBlobs(so))
        {
            var blobUri = blob.Name;
            var blobIdentifier = FileNameUtility.GetRelativeUri(blobUri, Root);
            identifiers.Add(blobIdentifier);
        }

        var recursive = so.Recursive;
        var relativeFolderUri = FileNameUtility.GetRelativeUri(so.FolderUri, Root);
        var foldersToExclude = GetFolders(relativeFolderUri).Concat([relativeFolderUri]).ToArray();
        var filteredFiles = identifiers
            .Where(f => recursive || (FileNameUtility.GetRelativeFolder(f, Root) ?? string.Empty) == relativeFolderUri)
            .Where(f => so.Extensions == null || so.Extensions.Any(e => e.TrimStart('*') == Path.GetExtension(f)))
            .ToList();

        var files = filteredFiles;

        IEnumerable<string> result = new List<string>();
        var onlyListFiles = so.Type == FileEntryTypes.Files;
        if (onlyListFiles)
        {
            return files;
        }

        result = result.Concat(files);

        var listFolders = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Directories;
        if (listFolders)
        {
            var filesForFetchingFolders = so.Extensions == null
                ? identifiers
                : filteredFiles;
            var folders = filesForFetchingFolders
                .SelectMany(GetFolders)
                .Where(d => recursive || (FileNameUtility.GetRelativeFolder(d, Root) ?? string.Empty) == relativeFolderUri)
                .Except(foldersToExclude)
                .Distinct()
                .ToList();
            var onlyListFolders = so.Type == FileEntryTypes.Directories;
            if (onlyListFolders)
            {
                return folders;
            }

            result = result.Concat(folders);
        }

        return result
            .Distinct()
            .OrderBy(f => f)
            .ToList();
    }
#if NET10_0_OR_GREATER
    public async IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null)
    {
        foreach (var identifier in await List(so))
        {
            yield return identifier;
        }
    }
#endif

    protected IEnumerable<string> GetFolders(string uri)
    {
        var folder = FileNameUtility.GetRelativeFolder(uri, Root);
        if (folder != null && folder != Root)
        {
            foreach (var parent in GetFolders(folder))
            {
                yield return parent;
            }
            yield return folder;
        }
    }

    protected async IAsyncEnumerable<BlobItem> ListBlobs(FileSearchObject so)
    {
        var relativeFolderUri = FileNameUtility.GetRelativeUri(so.FolderUri, Root);
        var blobPages = Container.GetBlobsAsync(BlobTraits.None, BlobStates.None, relativeFolderUri, CancellationToken.None);
        await foreach (var blob in blobPages)
        {
            yield return blob;
        }
    }
    protected async Task<Response<BlobDownloadResult>?> Download(string identifier)
    {
        await communicator.Open();
        var blob = GetBlobReference(identifier);
        if (!await blob.ExistsAsync())
        {
            return null;
        }

        return await blob.DownloadContentAsync();
    }

    public async Task Move(string sourceIdentifier, string targetIdentifier)
    {
        await communicator.Open();
        var sourceBlob = GetBlobReference(sourceIdentifier);
        var targetBlob = GetBlobReference(targetIdentifier);
        // server-side copy is asynchronous on Azure's side; wait for completion before deleting the source.
        // The copy source is the address the SDK built for the blob, escaped: a ?, # or % in its name stays part of it
        var copyOperation = await targetBlob.StartCopyFromUriAsync(sourceBlob.Uri);
        await copyOperation.WaitForCompletionAsync();
        await sourceBlob.DeleteIfExistsAsync();
    }
    public async Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
    {
        await communicator.Open();
        var blob = GetBlobReference(identifier);
        var headers = new BlobHttpHeaders { ContentType = GetContentType(identifier, contentType) };
        await blob.UploadAsync(new BinaryData(bytes), new BlobUploadOptions { HttpHeaders = headers });
        return FileNameUtility.GetRelativeUri(identifier, Root);
    }
    public async Task<string> Save(string identifier, Stream stream, string? contentType = null)
    {
        await communicator.Open();
        var blob = GetBlobReference(identifier);
        var headers = new BlobHttpHeaders { ContentType = GetContentType(identifier, contentType) };
        stream.Position = 0;
        await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });
        return FileNameUtility.GetRelativeUri(identifier, Root);
    }
    public async Task Delete(string identifier)
    {
        await communicator.Open();
        var blob = GetBlobReference(identifier);
        await blob.DeleteIfExistsAsync();
    }



    public string GetAbsoluteUri(string identifier)
    {
        communicator.Open().GetAwaiter().GetResult();
        var uri = FileNameUtility.GetUri(identifier, Root);
        if (communicator.Contained)
        {
            // read as a URL, as GetBlobUri reads it: %2e is a dot there, though the SDK keeps it in a blob's name
            UrlPath.EnsureContained(new Uri(uri), Container.Uri);
        }
        return uri;
    }

    public string GetIdentifier(string uri)
    {
        communicator.Open().GetAwaiter().GetResult();
        return FileNameUtility.GetRelativeUri(uri, Root);
    }

    public string? GetRelativeFolder(string identifier)
    {
        communicator.Open().GetAwaiter().GetResult();
        return FileNameUtility.GetRelativeFolder(identifier, Root);
    }

    public Uri GetBlobUri(string identifier)
        => new(GetAbsoluteUri(identifier));
    protected internal BlobClient GetBlobReference(string identifier)
    {
        var prefixedFilename = FileNameUtility.GetRelativeUri(identifier, Root);
        var blob = Container.GetBlobClient(prefixedFilename);
        if (communicator.Contained)
        {
            // the SDK resolves the dot segments of the address it builds, so ../ can name a blob in another container
            UrlPath.EnsureContained(blob.Uri, Container.Uri);
        }
        return blob;
    }
    [Obsolete("A BlobClient carries no content type: Save sets it on upload. Use GetBlobReference(identifier).")]
    protected internal BlobClient GetBlobReference(string identifier, string? contentType = null)
        => GetBlobReference(identifier);

    /// <summary>The content type the caller gives, or else the one the identifier's extension implies.</summary>
    private static string GetContentType(string identifier, string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? ContentTypeUtility.GetContentType(identifier) : contentType;

}