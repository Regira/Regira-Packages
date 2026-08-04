using Regira.IO.Storage.Abstractions;

namespace Regira.IO.Storage;

public class FileSearchObject
{
    public string? FolderUri { get; set; }
    public ICollection<string>? Extensions { get; set; }
    public bool Recursive { get; set; } = false;
    public FileEntryTypes Type { get; set; } = FileEntryTypes.All;
}