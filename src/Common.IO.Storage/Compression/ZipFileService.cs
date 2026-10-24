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
    public Task<StoredFileInfo?> GetInfo(string identifier)
    {
        var entry = ZipArchive.Find(identifier);
        // a folder's entry has no name of its own
        if (entry == null || entry.Name.Length == 0)
        {
            return Task.FromResult<StoredFileInfo?>(null);
        }

        // the size the archive records, so the entry is not inflated to measure it; an entry of an archive open for
        // update has no Length once it was opened (read, or written by Save), and then its stream, held in memory, has one
        long length;
        try
        {
            length = entry.Length;
        }
        catch (InvalidOperationException)
        {
            using var stream = entry.Open();
            length = stream.Length;
        }
        return Task.FromResult<StoredFileInfo?>(new StoredFileInfo(length, entry.LastWriteTime.UtcDateTime));
    }
    public Task<IEnumerable<string>> List(FileSearchObject? so = null)
    {
        so ??= new FileSearchObject();

        // the folder itself, whole: dir2/dir2.1 does not reach into dir2/dir2.10
        var folder = string.IsNullOrWhiteSpace(so.FolderUri) ? "" : ZipUtility.NormalizePath(so.FolderUri)!.TrimEnd('/');
        var prefix = folder.Length > 0 ? folder + '/' : "";
        // below the folder, and directly in it unless Recursive
        bool IsListed(string path) => path.Length > prefix.Length
            && path.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)
            && (so.Recursive || path.IndexOf('/', prefix.Length) < 0);
        var hasExtensions = so.Extensions?.Any() == true;

        var listFiles = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Files;
        var listDirectories = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Directories;

        // as Unzip names them: an archive another tool wrote may separate its folders with '\'
        var paths = ZipArchive.Entries.Select(e => ZipUtility.NormalizePath(e.FullName)).ToArray();
        // a folder's own entry ends in '/'
        var files = paths
            .Where(p => !p.EndsWith('/') && IsListed(p))
            .Where(p => !hasExtensions || so.Extensions!.Any(p.EndsWith))
            .ToArray();

        IEnumerable<string> result = [];
        if (listFiles)
        {
            result = result.Concat(files);
        }
        if (listDirectories)
        {
            // an archive need not hold an entry per folder: the folders are those its entries lie in
            var folders = paths.SelectMany(GetFolders).Distinct().Where(IsListed);
            if (hasExtensions)
            {
                // a folder holds a listed file below it: dir2.1 holds nothing of dir2.10
                folders = folders.Where(d => files.Any(f => f.StartsWith(d + '/', StringComparison.Ordinal)));
            }
            result = result.Concat(folders);
        }

        return Task.FromResult<IEnumerable<string>>(result.OrderBy(f => f).ToArray());
    }
    // every folder a path lies in, outermost first; a folder's own entry ("dir/") names that folder too
    private static IEnumerable<string> GetFolders(string path)
    {
        for (var end = path.IndexOf('/'); end > 0; end = path.IndexOf('/', end + 1))
        {
            yield return path.Substring(0, end);
        }
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

    public Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
    {
        var file = bytes.ToBinaryFile(contentType);
        file.Identifier = identifier;
        ZipArchive.AddFile(file);
        return Task.FromResult(ZipUtility.NormalizePath(identifier)!);
    }
    public Task<string> Save(string identifier, Stream stream, string? contentType = null)
    {
        var file = stream.ToBinaryFile(contentType);
        file.Identifier = identifier;
        ZipArchive.AddFile(file);
        return Task.FromResult(ZipUtility.NormalizePath(identifier)!);
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
        // a target naming the same entry, whatever its separators, leaves it where it is
        if (entry == null || ZipUtility.NormalizePath(entry.FullName) == ZipUtility.NormalizePath(targetIdentifier))
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