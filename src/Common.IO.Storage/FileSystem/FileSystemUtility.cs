using Regira.IO.Abstractions;
using Regira.IO.Models;
using Regira.IO.Utilities;

namespace Regira.IO.Storage.FileSystem;

public static class FileSystemUtility
{
    public static async Task<IMemoryFile?> Parse(string path)
    {
        return await ParseToByteFile(path);
    }
    public static async Task<INamedFile?> ParseToByteFile(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        return new BinaryFileItem
        {
            Bytes = bytes,
            Length = bytes.Length,
            FileName = Path.GetFileName(path),
            ContentType = ContentTypeUtility.GetContentType(path)
        };
    }
    public static Task<INamedFile?> ParseToStreamFile(string path)
    {
        if (!File.Exists(path))
        {
            return Task.FromResult((INamedFile?)null);
        }
        var stream = File.OpenRead(path);
        return Task.FromResult((INamedFile?)new BinaryFileItem
        {
            Stream = stream,
            Length = stream.Length,
            FileName = Path.GetFileName(path),
            ContentType = ContentTypeUtility.GetContentType(path)
        });
    }

    public static async Task<FileInfo> SaveStream(string path, Stream stream)
    {
        var file = new FileInfo(path);
        await using var fileStream = file.Create();
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);

        return file;
    }
    // https://stackoverflow.com/questions/876473/is-there-a-way-to-check-if-a-file-is-in-use
    public static bool IsFileLocked(string filename, FileAccess fileAccess = FileAccess.Read)
    {
        FileStream? stream = null;

        try
        {
            stream = File.Open(filename, FileMode.Open, fileAccess, FileShare.None);
        }
        catch (IOException)
        {
            //the file is unavailable because it is:
            //still being written to
            //or being processed by another thread
            //or does not exist (has already been processed)
            return true;
        }
        finally
        {
            stream?.Close();
            stream?.Dispose();
        }

        return false;
    }
}