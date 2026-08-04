using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Utilities;
using System.IO.Compression;

namespace Regira.IO.Storage.Compression;

public class ZipFileService(ZipFileCommunicator communicator) : IFileService, IDisposable
{
    public string Root => string.Empty;
    public string RootFolder => Root;
    private BinaryFileItem? _sourceFile;
    protected IMemoryFile SourceFile
        => _sourceFile ??= new MemoryStream();
    private ZipArchive? _zipArchive;
    protected ZipArchive ZipArchive
        => _zipArchive ??= (communicator.SourceFile ?? SourceFile)
                .ToBinaryFile($"{Guid.NewGuid()}.zip")
                .ToZipArchive();

    public Task<bool> Exists(string identifier) => Task.FromResult(ZipArchive.Find(identifier) != null);
    public Task<Stream?> GetStream(string identifier)
    {
        var entry = ZipArchive.Find(identifier);
        return Task.FromResult(entry?.Open());
    }
    public async Task<byte[]?> GetBytes(string identifier)
    {
#if NETCOREAPP3_1_OR_GREATER
        await using var stream = await GetStream(identifier);
#else
        using var stream = await GetStream(identifier);
#endif
        return stream.GetBytes();
    }
    public Task<IEnumerable<string>> List(FileSearchObject? so = null)
    {
        var identifiers = ZipArchive.Entries
            .Select(e => e.FullName);

        if (so != null)
        {
            if (!string.IsNullOrWhiteSpace(so.FolderUri))
            {
                var folderUri = so.FolderUri!.TrimStart('/');
                identifiers = identifiers.Where(x => x.TrimStart('/').StartsWith(folderUri, StringComparison.InvariantCultureIgnoreCase));
            }
            if (so.Extensions?.Any() == true)
            {
                identifiers = identifiers.Where(x => so.Extensions.Any(x.EndsWith));
            }
        }

        return Task.FromResult(identifiers);
    }
#if NET10_0_OR_GREATER
    public async IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null)
    {
        IEnumerable<string> identifiers = ZipArchive.Entries.Select(e => e.FullName);

        if (so != null)
        {
            if (!string.IsNullOrWhiteSpace(so.FolderUri))
            {
                var folderUri = so.FolderUri!.TrimStart('/');
                identifiers = identifiers.Where(x => x.TrimStart('/').StartsWith(folderUri, StringComparison.InvariantCultureIgnoreCase));
            }
            if (so.Extensions?.Any() == true)
            {
                identifiers = identifiers.Where(x => so.Extensions.Any(x.EndsWith));
            }
        }

        foreach (var identifier in identifiers)
            yield return identifier;
    }
#endif

    public Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
    {
        var file = bytes.ToBinaryFile(contentType);
        file.Identifier = identifier;
        ZipArchive.AddFile(file);
        return Task.FromResult(identifier);
    }
    public Task<string> Save(string identifier, Stream stream, string? contentType = null)
    {
        var file = stream.ToBinaryFile(contentType);
        file.Identifier = identifier;
        ZipArchive.AddFile(file);
        return Task.FromResult(identifier);
    }
    public Task Delete(string identifier)
    {
        var entry = ZipArchive.Find(identifier);
        entry?.Delete();
        return Task.CompletedTask;
    }

    public Task Move(string sourceIdentifier, string targetIdentifier)
    {
        var entry = ZipArchive.Find(sourceIdentifier);
        if (entry == null)
        {
            return Task.CompletedTask;
        }

        var file = entry.ToBinaryFile();
        file.Identifier = targetIdentifier;
        ZipArchive.AddFile(file);

        entry.Delete();

        return Task.CompletedTask;
    }

    public string GetAbsoluteUri(string identifier) => identifier;
    public string GetIdentifier(string uri) => uri;
    public string? GetRelativeFolder(string identifier)
    {
        var pos = identifier.LastIndexOf("/", StringComparison.Ordinal);
        return pos > -1
            ? identifier.Substring(0, pos).TrimEnd('/') + '/'
            : null;
    }

    public void Dispose()
    {
        _zipArchive?.Dispose();
        _sourceFile?.Dispose();
    }
}