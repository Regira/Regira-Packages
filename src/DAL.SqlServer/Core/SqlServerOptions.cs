namespace Regira.DAL.SqlServer.Core;

public class SqlServerOptions
{
    public SqlServerSettings? DbSettings { get; set; }
    public string? ConnectionString { get; set; }
    /// <summary>
    /// Restore over a database that already exists: it is dropped first
    /// </summary>
    public bool Overwrite { get; set; }
    /// <summary>
    /// Directory where SQL Server writes (backup) and reads (restore) the .bak file, as the SQL Server instance sees it.
    /// SQL Server's service account needs read and write access to it.
    /// </summary>
    public string BackupDirectory { get; set; } = null!;
    /// <summary>
    /// <see cref="BackupDirectory"/> as this process sees it: a UNC share, or the host path of a container volume.
    /// Leave empty when SQL Server runs on this machine.
    /// </summary>
    public string? LocalBackupDirectory { get; set; }
}
