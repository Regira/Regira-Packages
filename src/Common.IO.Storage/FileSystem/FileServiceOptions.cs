namespace Regira.IO.Storage.FileSystem;

public class FileSystemOptions
{
    public string RootFolder { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets a value indicating whether the item is contained within its root context.
    /// </summary>
    public bool Contained { get; set; } = true;
}
