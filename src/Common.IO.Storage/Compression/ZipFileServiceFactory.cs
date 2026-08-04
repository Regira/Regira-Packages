using Regira.IO.Abstractions;

namespace Regira.IO.Storage.Compression;

public class ZipFileServiceFactory()
{
    public ZipFileService Create(IMemoryFile? sourceFile = null, string? password = null) 
        => new ZipFileService(new ZipFileCommunicator { SourceFile = sourceFile, Password = password });
}
