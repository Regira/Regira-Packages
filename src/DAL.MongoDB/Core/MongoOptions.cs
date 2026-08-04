namespace Regira.DAL.MongoDB.Core;

public class MongoOptions
{
    public MongoSettings? DbSettings { get; set; }
    public string? ConnectionString { get; set; }
    public bool Overwrite { get; set; }
    /// <summary>
    /// Path where to find the backing up and restore exe-files
    /// </summary>
    public string ToolsDirectory { get; set; } = null!;
}