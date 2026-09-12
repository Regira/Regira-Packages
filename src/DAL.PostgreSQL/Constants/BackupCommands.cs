namespace Regira.DAL.PostgreSQL.Constants;

public class BackupCommands
{
    public static string SchemaBackup => @"""{ProcessPath}"" --host {Host} --port {Port} --username ""{Username}"" --no-password --format custom --verbose --file ""{TargetPath}"" {SchemasArgs} ""{SourceDb}""";
    public static string FullBackup => @"""{ProcessPath}"" --host {Host} --port {Port} --username ""{Username}"" --no-password --format custom --blobs --verbose --file ""{TargetPath}"" ""{SourceDb}""";
    /// <summary>
    /// Reads an archive's table of contents. Touches no server, so it answers whether the file is a readable
    /// archive before anything is done to the target database.
    /// </summary>
    public static string ListArchive => @"""{ProcessPath}"" --list ""{SourcePath}""";
    public static string Restore => @"""{ProcessPath}"" --host {Host} --port {Port} --username ""{Username}"" --dbname ""{TargetDb}"" --no-password  --verbose ""{SourcePath}""";
}