namespace Regira.DAL.MongoDB.Constants;

/// <summary>
/// Argument templates for the MongoDB Database Tools. The executable itself is not part of the template:
/// both services start it directly, so no shell reads these arguments.
/// </summary>
/// <remarks>
/// <c>{ConfigPath}</c> is the YAML file holding the connection URI and the password — see <c>MongoToolConfigFile</c>
/// — so neither appears among the arguments.
/// </remarks>
public class BackupCommands
{
    // https://www.mongodb.com/docs/database-tools/mongodump/
    public static string Backup => @"--gzip --archive=""{TargetPath}"" --config=""{ConfigPath}""";

    // https://www.mongodb.com/docs/database-tools/mongorestore/
    public static string Restore => @"--gzip --archive=""{SourcePath}"" --config=""{ConfigPath}""";
}
