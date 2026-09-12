namespace Regira.DAL.PostgreSQL.Core;

public class PgOptions
{
    public PgSettings? DbSettings { get; set; }
    public string? ConnectionString { get; set; }
    public ICollection<string>? BackupSchemas { get; set; }
    /// <summary>
    /// Replaces the target database when it already exists: it is dropped and recreated, so anything the
    /// backup does not contain is lost. Without it, restoring onto an existing database fails.
    /// </summary>
    public bool Overwrite { get; set; }
    /// <summary>
    /// Database to connect to when creating or dropping the target database.
    /// Defaults to <see cref="PgDefaults.MaintenanceDatabase"/>.
    /// </summary>
    public string? MaintenanceDatabase { get; set; }
    /// <summary>
    /// Path where to find the backing up and restore exe-files
    /// </summary>
    public string ToolsDirectory { get; set; } = null!;
}