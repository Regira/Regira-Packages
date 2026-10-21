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
    /// An implementation that does not provide its own falls back to reading the content to measure it, and leaves
    /// <see cref="StoredFileInfo.LastModified"/> <c>null</c>.
    /// </remarks>
    async Task<StoredFileInfo?> GetInfo(string identifier)
    {
        var bytes = await GetBytes(identifier);
        return bytes != null ? new StoredFileInfo(bytes.LongLength, null) : null;
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