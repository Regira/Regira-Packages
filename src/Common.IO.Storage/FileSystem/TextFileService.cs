namespace Regira.IO.Storage.FileSystem;

public class TextFileService(FileSystemOptions options) : BinaryFileService(options), Abstractions.ITextFileService
{
    public Task<string?> GetContents(string identifier)
    {
        var uri = ResolveUri(identifier);
        if (!File.Exists(uri))
        {
            return Task.FromResult((string?)null);
        }
        var content = File.ReadAllText(uri);
        return Task.FromResult(content)!;
    }
    public Task<string> Save(string identifier, string contents, string? contentType = null)
    {
        var uri = ResolveUri(identifier);
        File.WriteAllText(uri, contents);
        return Task.FromResult(GetIdentifier(uri));
    }
}