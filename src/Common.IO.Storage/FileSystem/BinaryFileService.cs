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

        IEnumerable<string> result = Array.Empty<string>();

        if (Directory.Exists(absoluteFolderUri))
        {
            // files
            var files = Directory.EnumerateFiles(absoluteFolderUri, extensions, searchOptions)
                .Select(fileUri => FileNameUtility.GetRelativeUri(fileUri, Root))
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
                    .Select(dirUri => FileNameUtility.GetRelativeUri(dirUri, Root));
                if (so.Extensions != null)
                {
                    folders = folders.Where(d => fileList?.Any(f => f.StartsWith(d)) == true);
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
        // iterator body is deferred — connects (and surfaces failures) on first MoveNextAsync, not at the call
        _communicator?.EnsureConnected();
        so ??= new FileSearchObject();

        var folderUri = so.FolderUri ?? "";
        var recursive = so.Recursive;
        var extensions = "*";

        var listFiles = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Files;
        var listDirectories = so.Type == FileEntryTypes.All || so.Type == FileEntryTypes.Directories;

        var searchOptions = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var absoluteFolderUri = ResolveUri(folderUri);

        if (Directory.Exists(absoluteFolderUri))
        {
            // Enumerate files
            if (listFiles)
            {
                var files = Directory.EnumerateFiles(absoluteFolderUri, extensions, searchOptions)
                    .Select(fileUri => FileNameUtility.GetRelativeUri(fileUri, Root))
                    .Where(f => so.Extensions == null || so.Extensions.Any(e => e.TrimStart('*') == Path.GetExtension(f)));

                foreach (var file in files)
                {
                    yield return file;
                }
            }

            // Enumerate directories
            if (listDirectories)
            {
                var directories = Directory.EnumerateDirectories(absoluteFolderUri, "*", searchOptions)
                    .Select(dirUri => FileNameUtility.GetRelativeUri(dirUri, Root));

                foreach (var directory in directories)
                {
                    yield return directory;
                }
            }
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
    public Task Delete(string identifier)
    {
        _communicator?.EnsureConnected();
        var uri = ResolveUri(identifier);
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
        => FileNameUtility.GetRelativeUri(uri, Root).Replace('\\', '/');
    public string? GetRelativeFolder(string identifier)
        => FileNameUtility.GetRelativeFolder(identifier, Root);


    public void CreateDirectory(string uri)
    {
        _communicator?.EnsureConnected();
        var dirPath = Path.GetDirectoryName(uri);
        Directory.CreateDirectory(dirPath ?? throw new InvalidOperationException());
    }
    private string ResolveUri(string identifier)
    {
        var uri = FileNameUtility.GetAbsoluteUri(identifier, Root);
        return options.Contained && !string.IsNullOrWhiteSpace(Root)
            ? FileNameUtility.EnsureContained(uri, Root)
            : uri;
    }
}
