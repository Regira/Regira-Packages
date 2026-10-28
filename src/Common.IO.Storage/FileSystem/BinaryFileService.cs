using Regira.IO.Storage.Abstractions;

namespace Regira.IO.Storage.FileSystem;

public class BinaryFileService(FileSystemOptions options) : IFileService
{
    [Obsolete("Please use FileSystemOptions instead", false)]
    public class FileServiceOptions : FileSystemOptions;

    private readonly NetworkShareCommunicator? _communicator;
    // private protected: keeps this type single-ctor for DI (a second public ctor makes
    // constructor selection ambiguous) — consumers use NetworkFileService instead
    private protected BinaryFileService(NetworkShareCommunicator communicator)
        : this(communicator.Options)
    {
        _communicator = communicator;
    }

    public string Root => options.RootFolder;


    public Task<bool> Exists(string identifier)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        var exists = File.Exists(uri) || Directory.Exists(uri);
        return Task.FromResult(exists);
    }
    public Task<byte[]?> GetBytes(string identifier)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        if (!File.Exists(uri))
        {
            return Task.FromResult((byte[]?)null);
        }
        var bytes = File.ReadAllBytes(uri);
        return Task.FromResult(bytes)!;
    }
    public Task<Stream?> GetStream(string identifier)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        if (!File.Exists(uri))
        {
            return Task.FromResult((Stream?)null);
        }
        var stream = (Stream)File.OpenRead(uri);
        return Task.FromResult(stream)!;
    }
    public Task<StoredFileInfo?> GetInfo(string identifier)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        // File.Exists first, as GetBytes checks: FileInfo trims a trailing separator ("a.txt/" named the file on Windows)
        // and throws on a null character
        if (!File.Exists(uri))
        {
            return Task.FromResult<StoredFileInfo?>(null);
        }
        var file = new FileInfo(uri);
        return Task.FromResult(file.Exists ? new StoredFileInfo(file.Length, file.LastWriteTimeUtc) : null);
    }
    public Task<IEnumerable<string>> List(FileSearchObject? so = null)
    {
        _communicator?.EnsureConnected();
        so ??= new FileSearchObject();

        var folderUri = so.FolderUri ?? string.Empty;
        var recursive = so.Recursive;
        var extensions = "*";//so.Extensions?.Any() ?? false ? string.Join(",", so.Extensions) : "*.*";

        var listFiles = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Files;
        var listDirectories = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Directories;

        var searchOptions = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var absoluteFolderUri = ResolveUri(folderUri);
        var root = ResolvedRoot;

        IEnumerable<string> result = Array.Empty<string>();

        if (Directory.Exists(absoluteFolderUri))
        {
            // files
            var files = Directory.EnumerateFiles(absoluteFolderUri, extensions, searchOptions)
                .Select(fileUri => ToIdentifier(FileNameUtility.GetRelativeUri(fileUri, root)))
                .Where(f => so.Extensions == null || so.Extensions.Any(e => e.TrimStart('*') == Path.GetExtension(f)));
            string[]? fileList = null;
            if (listDirectories && so.Extensions != null)
            {
                fileList = files.ToArray();
            }
            if (listFiles)
            {
                result = result.Concat(fileList ?? files);
            }
            // directories
            if (listDirectories)
            {
                var folders = Directory.EnumerateDirectories(absoluteFolderUri, "", searchOptions)
                    .Select(dirUri => ToIdentifier(FileNameUtility.GetRelativeUri(dirUri, root)));
                if (so.Extensions != null)
                {
                    // a folder holds a file below it: dir2.1 holds nothing of dir2.10
                    folders = folders.Where(d => fileList?.Any(f => f.StartsWith(d + '/')) == true);
                }
                result = result.Concat(folders);
            }

            result = result
                .OrderBy(f => f);
        }

        return Task.FromResult(result);
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

    public Task Move(string sourceIdentifier, string targetIdentifier)
    {
        _communicator?.EnsureConnected();
        var sourceUri = ResolveUri(sourceIdentifier);
        var targetUri = ResolveUri(targetIdentifier);
        File.Move(sourceUri, targetUri);
        return Task.CompletedTask;
    }
    public Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        CreateDirectory(uri);
        File.WriteAllBytes(uri, bytes);
        return Task.FromResult(GetIdentifier(uri));
    }
    public Task<string> Save(string identifier, Stream stream, string? contentType = null)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        CreateDirectory(uri);
        using (var fileStream = File.Create(uri))
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(fileStream);
        }

        return Task.FromResult(GetIdentifier(uri));
    }
    /// <exception cref="ArgumentException">The identifier resolves to the root (<c>""</c>, <c>"."</c>, <c>"x/.."</c>)</exception>
    public Task Delete(string identifier)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
        // the root is within itself, so containment lets it through: refused, as the SFTP backend refuses it
        if (IsRoot(uri))
        {
            throw new ArgumentException("The identifier resolves to the root, which is not deleted.", nameof(identifier));
        }
        if (File.Exists(uri))
        {
            File.Delete(uri);
        }
        else if (Directory.Exists(uri))
        {
            var recursiveDelete = Directory.GetFiles(uri, "*", SearchOption.AllDirectories).Length == 0;
            Directory.Delete(uri, recursiveDelete);
        }

        return Task.CompletedTask;
    }

    public string GetAbsoluteUri(string identifier)
        => ResolveUri(identifier);
    public string GetIdentifier(string uri)
        // use forward slashes to keep identifier uniform with other IFileService implementations (e.g. Azure)
        => FileNameUtility.GetRelativeUri(Locate(uri), ResolvedRoot).Replace('\\', '/');
    // the OS separator becomes '/', as Save answers on every platform; a '\' in a Linux file name stays part of the name
    private static string ToIdentifier(string relativeUri)
        => Path.DirectorySeparatorChar == '/' ? relativeUri : relativeUri.Replace(Path.DirectorySeparatorChar, '/');
    public string? GetRelativeFolder(string identifier)
        => FileNameUtility.GetRelativeFolder(Locate(identifier), ResolvedRoot);


    public void CreateDirectory(string uri)
    {
        _communicator?.EnsureConnected();
        var dirPath = Path.GetDirectoryName(uri);
        Directory.CreateDirectory(dirPath ?? throw new InvalidOperationException());
    }
    private protected string ResolveUri(string identifier)
    {
        var uri = FileNameUtility.GetAbsoluteUri(identifier, Root);
        return IsContained
            ? FileNameUtility.EnsureContained(uri, FileNameUtility.ConvertForwardSlashes(Root))
            : uri;
    }

    // EnsureContained answers Path.GetFullPath, so a contained service strips its root written out in full, from a path
    // written out the same way: as configured, a root with a '/' on Windows, dot segments or relative to the working
    // directory did not match, and the absolute path passed for the identifier. Both separators are separators in the
    // root, as in an identifier: on Linux a '\' would otherwise be part of a folder's name.
    private bool IsContained => options.Contained && !string.IsNullOrWhiteSpace(Root);
    private bool IsRoot(string path)
        => !string.IsNullOrWhiteSpace(Root) && string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(FileNameUtility.ConvertForwardSlashes(Root))),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private string ResolvedRoot => IsContained ? Path.GetFullPath(FileNameUtility.ConvertForwardSlashes(Root)) : Root;
    /// <summary>Where <paramref name="uri"/> lands, as <see cref="ResolveUri"/> spells it, without refusing a path outside the root.</summary>
    private string Locate(string uri)
    {
        var path = FileNameUtility.GetAbsoluteUri(uri, Root);
        return IsContained ? Path.GetFullPath(path) : path;
    }
}
