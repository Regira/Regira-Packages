namespace Regira.IO.Storage.Abstractions;

public interface IFileService
{
    string Root { get; }

    Task<bool> Exists(string identifier);
    Task<byte[]?> GetBytes(string identifier);
    Task<Stream?> GetStream(string identifier);
    /// <summary>
    /// The size and last-modified time of the file <paramref name="identifier"/> names, read without its content;
    /// <c>null</c> when no file has that identifier, a folder included.
    /// </summary>
    /// <remarks>
    /// An implementation that does not provide its own falls back to opening the content with <see cref="GetStream"/>
    /// and measuring it, reading it through unless the stream knows its length, and leaves
    /// <see cref="StoredFileInfo.LastModified"/> <c>null</c>. It answers <c>null</c> when <see cref="GetStream"/> does: a
    /// store whose <see cref="GetStream"/> throws for a missing file or a folder implements <c>GetInfo</c> itself.
    /// </remarks>
    async Task<StoredFileInfo?> GetInfo(string identifier)
    {
        // measured in passing, so a large file is never held in memory whole
        await using var stream = await GetStream(identifier);
        if (stream == null)
        {
            return null;
        }
        if (stream.CanSeek)
        {
            return new StoredFileInfo(stream.Length, null);
        }
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            length += read;
        }
        return new StoredFileInfo(length, null);
    }
    Task<IEnumerable<string>> List(FileSearchObject? so = null);
#if NET10_0_OR_GREATER
    IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null);
#endif


    Task Move(string sourceIdentifier, string targetIdentifier);
    /// <summary>Saves the content and returns the (relative) identifier of the stored file.</summary>
    Task<string> Save(string identifier, byte[] bytes, string? contentType = null);
    /// <summary>Saves the content and returns the (relative) identifier of the stored file.</summary>
    Task<string> Save(string identifier, Stream stream, string? contentType = null);
    Task Delete(string identifier);

    string GetAbsoluteUri(string identifier);
    string GetIdentifier(string uri);
    string? GetRelativeFolder(string identifier);
}