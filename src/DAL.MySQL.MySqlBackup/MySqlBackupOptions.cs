using Regira.DAL.MySQL.Models;

namespace Regira.DAL.MySQL.MySqlBackup;

public class MySqlBackupOptions
{
    public MySqlSettings? DbSettings { get; set; }
    public string? ConnectionString { get; set; }
}