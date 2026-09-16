namespace Regira.DAL.PostgreSQL.Constants;

/// <summary>
/// Argument templates for <c>pg_dump</c> and <c>pg_restore</c>. The executable itself is not part of the template:
/// the services start it directly, so no shell reads these arguments. A value placed between the template's quotes
/// has its own quotes and the backslashes before them escaped first.
/// </summary>
public class BackupCommands
{
    public static string SchemaBackup => @"--host ""{Host}"" --port {Port} --username ""{Username}"" --no-password --format custom --verbose --file ""{TargetPath}"" {SchemasArgs} ""{SourceDb}""";
    public static string FullBackup => @"--host ""{Host}"" --port {Port} --username ""{Username}"" --no-password --format custom --blobs --verbose --file ""{TargetPath}"" ""{SourceDb}""";
    /// <summary>
    /// Reads an archive's table of contents. Touches no server, so it answers whether the file is a readable
    /// archive before anything is done to the target database.
    /// </summary>
    public static string ListArchive => @"--list ""{SourcePath}""";
    public static string Restore => @"--host ""{Host}"" --port {Port} --username ""{Username}"" --dbname ""{TargetDb}"" --no-password --verbose ""{SourcePath}""";
}
