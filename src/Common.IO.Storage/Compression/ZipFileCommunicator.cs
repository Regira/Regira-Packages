using Regira.IO.Abstractions;

namespace Regira.IO.Storage.Compression;

public class ZipFileCommunicator
{
    public IMemoryFile? SourceFile { get; set; }
    public string? Password { get; set; }
}
