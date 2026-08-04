namespace Regira.DAL.PostgreSQL.Core;

public class PgOptions
{
    public PgSettings? DbSettings { get; set; }
    public string? ConnectionString { get; set; }
    public ICollection<string>? BackupSchemas { get; set; }
    public bool Overwrite { get; set; }
    /// <summary>
    /// Path where to find the backing up and restore exe-files
    /// </summary>
    public string ToolsDirectory { get; set; } = null!;
}