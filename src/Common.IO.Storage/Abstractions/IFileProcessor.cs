namespace Regira.IO.Storage.Abstractions;

public interface IFileProcessor
{
    Task ProcessFiles(FileSearchObject so, Func<string, IFileService, Task> handleFile, bool? processRecursively = null);
}