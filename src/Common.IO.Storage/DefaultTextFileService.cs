using System.Text;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Utilities;

namespace Regira.IO.Storage;

public class DefaultTextFileService(IFileService binaryFileService, Encoding? encoding = null) : ITextFileService
{
    public string Root => binaryFileService.Root;
    public string RootFolder => Root;


    public async Task<string?> GetContents(string identifier)
    {
        using var stream = await GetStream(identifier);
        return FileUtility.GetString(stream);
    }
    public Task<string> Save(string identifier, string contents, string? contentType = null)
    {
        using var stream = FileUtility.GetStreamFromString(contents, encoding);
        return Save(identifier, stream, contentType);
    }


    public Task<bool> Exists(string identifier)
        => binaryFileService.Exists(identifier);
    public Task<byte[]?> GetBytes(string identifier)
        => binaryFileService.GetBytes(identifier);
    public Task<Stream?> GetStream(string identifier)
        => binaryFileService.GetStream(identifier);
    public Task<IEnumerable<string>> List(FileSearchObject? so = null)
        => binaryFileService.List(so);
#if NET10_0_OR_GREATER
    public IAsyncEnumerable<string> ListAsync(FileSearchObject? so = null)
        => binaryFileService.ListAsync(so);
#endif

    public Task Move(string sourceIdentifier, string targetIdentifier)
        => binaryFileService.Move(sourceIdentifier, targetIdentifier);
    public Task<string> Save(string identifier, byte[] bytes, string? contentType = null)
        => binaryFileService.Save(identifier, bytes, contentType);
    public Task<string> Save(string identifier, Stream stream, string? contentType = null)
        => binaryFileService.Save(identifier, stream, contentType);
    public Task Delete(string identifier)
        => binaryFileService.Delete(identifier);

    public string GetAbsoluteUri(string identifier)
        => binaryFileService.GetAbsoluteUri(identifier);
    public string GetIdentifier(string uri)
        => binaryFileService.GetIdentifier(uri);
    public string? GetRelativeFolder(string identifier)
        => binaryFileService.GetRelativeFolder(identifier);
}