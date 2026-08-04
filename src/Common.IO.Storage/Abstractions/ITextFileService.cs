namespace Regira.IO.Storage.Abstractions;

public interface ITextFileService : IFileService
{
    Task<string?> GetContents(string identifier);

    Task<string> Save(string identifier, string contents, string? contentType = null);
}