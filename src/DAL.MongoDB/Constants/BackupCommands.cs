namespace Regira.DAL.MongoDB.Constants;

// Example: https://blog.devgenius.io/backup-and-restore-mongodb-database-gz-d4871b8f5059
public class BackupCommands
{
    // https://www.mongodb.com/docs/database-tools/mongodump/
    public static string Backup => @"""{ProcessPath}"" --uri=""{Uri}"" --gzip --archive={TargetPath}";

    // https://www.mongodb.com/docs/database-tools/mongorestore/
    public static string Restore => @"""{ProcessPath}"" --uri=""{Uri}"" --gzip --archive={SourcePath}";
}