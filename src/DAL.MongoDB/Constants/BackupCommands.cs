namespace Regira.DAL.MongoDB.Constants;

/// <summary>
/// Argument templates for the MongoDB Database Tools. The executable itself is not part of the template:
/// both services start it directly, so no shell reads these arguments and a percent sign or an ampersand
/// in a percent-encoded URI survives intact.
/// </summary>
/// <remarks>
/// <c>{ConfigArgs}</c> is empty, or <c>--config</c> pointing at the file holding the password — see
/// <c>MongoPasswordFile</c>. It carries its own leading space so an unauthenticated command has no stray one.
/// </remarks>
public class BackupCommands
{
    // https://www.mongodb.com/docs/database-tools/mongodump/
    public static string Backup => @"--uri=""{Uri}"" --gzip --archive=""{TargetPath}""{ConfigArgs}";

    // https://www.mongodb.com/docs/database-tools/mongorestore/
    public static string Restore => @"--uri=""{Uri}"" --gzip --archive=""{SourcePath}""{ConfigArgs}";
}
