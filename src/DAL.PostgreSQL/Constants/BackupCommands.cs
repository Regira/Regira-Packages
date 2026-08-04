namespace Regira.DAL.PostgreSQL.Constants;

public class BackupCommands
{
    public static string SchemaBackup => @"""{ProcessPath}"" --host {Host} --port {Port} --username ""{Username}"" --no-password --format custom --verbose --file ""{TargetPath}"" {SchemasArgs} ""{SourceDb}""";
    public static string FullBackup => @"""{ProcessPath}"" --host {Host} --port {Port} --username ""{Username}"" --no-password --format custom --blobs --verbose --file ""{TargetPath}"" ""{SourceDb}""";
    public static string Restore => @"""{ProcessPath}"" --host {Host} --port {Port} --username ""{Username}"" --dbname ""{TargetDb}"" --no-password  --verbose ""{SourcePath}""";
}