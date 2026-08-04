using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Regira.DAL.MySQL.Models;

namespace Regira.DAL.MySQL.Services;

public class SQLDumpManager(MySqlSettings dbSettings, string? splitter)
{
    public record Result(string Output, string Failed);

    public event Action<string, object>? OnAction;

    public const string DefaultSplitter = "\r\n--\r\n";//" ----------------------------";
    public const string PrefixSQL = @"
SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;
SET GLOBAL log_bin_trust_function_creators = 1;
-- USE `GeefDatabaseName`;
";
    public const string SuffixSQL = @"
SET FOREIGN_KEY_CHECKS = 1;
SET GLOBAL log_bin_trust_function_creators = 0;
";

    private readonly string _splitter = splitter ?? DefaultSplitter;

    /// <summary>
    /// Preconditions:
    /// <list type="bullet">
    ///     <item>Remove foreign_key_checks (SET FOREIGN_KEY_CHECKS = 0)</item>
    ///     <item>Remove collation (SET NAMES utf8mb4)</item>
    ///     <item>Constructor param "splitter" is used to split queries, it is automatically wrapped by <see cref="Environment.NewLine">NewLines</see></item>
    /// </list>
    /// </summary>
    /// <param name="sqlDump"></param>
    /// <returns></returns>
    public async Task<Result> CorrectQuerySequence(string sqlDump)
    {
        sqlDump = Cleanup(sqlDump);

        var queries = sqlDump.Split(_splitter, StringSplitOptions.RemoveEmptyEntries).ToList();

        var output = new StringBuilder();
        var failed = new StringBuilder();


        var tmpDatabaseName = $"_temp_{Guid.NewGuid():N}";
        var tmpDbSettings = dbSettings.Clone<MySqlSettings>();
        tmpDbSettings.DatabaseName = null;

        try
        {
            var tmpConnectionString = tmpDbSettings.BuildConnectionString();
            await using (var createTmpDbConnection = new MySqlConnection(tmpConnectionString))
            {
                await createTmpDbConnection.ExecuteAsync($"CREATE SCHEMA {tmpDatabaseName};");
            }
            var queryDbSettings = dbSettings.Clone<MySqlSettings>();
            queryDbSettings.DatabaseName = tmpDatabaseName;
            var connectionString = queryDbSettings.BuildConnectionString(
                new("SslMode", "None"),
                new("Allow User Variables", "true"),
                new("Convert Zero Datetime", "True")
            );
            await using var dbConnection = new MySqlConnection(connectionString);
            await dbConnection.OpenAsync();

            await dbConnection.ExecuteAsync(PrefixSQL);
            output.AppendLine(PrefixSQL);

            var failedQueries = new List<string>();
            var errors = new List<string>();
            int success;
            var cycle = 1;
            do
            {
                success = 0;
                failedQueries.Clear();
                errors.Clear();
                OnAction?.Invoke("Cycle", cycle);
                for (var i = 0; i < queries.Count; i++)
                {
                    OnAction?.Invoke("Query", i);
                    var query = queries[i];
                    try
                    {
                        await dbConnection.ExecuteAsync(query);
                        var isFunction = query.Contains("CREATE FUNCTION", StringComparison.InvariantCultureIgnoreCase);
                        if (isFunction)
                        {
                            query = $@"delimiter;;
{query}
;;
delimiter;";
                        }
                        output.AppendLine($"{query}{Environment.NewLine}");
                        success++;
                    }
                    catch (Exception ex)
                    {
                        failedQueries.Add(query);
                        errors.Add(ex.Message);
                    }
                }

                cycle++;
                queries = failedQueries.ToList();
            } while (success > 0);

            await dbConnection.ExecuteAsync(SuffixSQL);
            output.AppendLine(SuffixSQL);

            await dbConnection.CloseAsync();

            for (var i = 0; i < failedQueries.Count; i++)
            {
                var query = failedQueries[i];
                var error = errors[i];
                failed
                    .AppendLine($"{_splitter}{Environment.NewLine}{query}")
                    .AppendLine($"-- ERROR: {error}")
                    .AppendLine();
            }
        }
        finally
        {
            await using var dropTmpDbConnection = new MySqlConnection(tmpDbSettings.BuildConnectionString());
            await dropTmpDbConnection.ExecuteAsync($"DROP SCHEMA IF EXISTS {tmpDatabaseName};");
        }

        return new Result(output.ToString(), failed.ToString());
    }

    public string Cleanup(string sqlContent)
    {
        var regOptions = RegexOptions.IgnoreCase;

        sqlContent = Regex.Replace(sqlContent, @"SET NAMES \w*;", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"SET FOREIGN_KEY_CHECKS = 0;", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"SET FOREIGN_KEY_CHECKS = 1;", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"CREATE ALGORITHM = UNDEFINED SQL SECURITY DEFINER VIEW", "CREATE VIEW", regOptions);
        sqlContent = Regex.Replace(sqlContent, @"DELIMITER (;)+(\s)*", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"(;)*( )*DELIMITER( )*(;)+", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"DROP \w* IF EXISTS [^;.]*;", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"SET @saved_cs_client\s*= (@|\w)*;*", string.Empty);
        sqlContent = Regex.Replace(sqlContent, @"SET character_set_client = (@|\w)*;*", string.Empty);
        sqlContent = Regex.Replace(sqlContent, @" CHARACTER SET \w* COLLATE \w*;?", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @" CHARACTER SET = \w*;?", string.Empty);
        sqlContent = Regex.Replace(sqlContent, @" COLLATE = \w*;?", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @" collate \w*;?", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @" CHARSET \w+", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @" CHARSET \w* COLLATE \w*;?", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @" AUTO_INCREMENT = [0-9]*", string.Empty, regOptions);
        sqlContent = Regex.Replace(sqlContent, @"convert\(date_format\((?<field>`[\w_ -]*`.`[\w_ -]*`),'%Y-%m-%d'\) using \w*\)", @"date_format(${field},'%Y-%m-%d')", regOptions);
        sqlContent = Regex.Replace(sqlContent, @"convert\((?<field>`[\w_ -]*`.`[\w_ -]*`) using \w*\)", @"${field}", regOptions);
        sqlContent = Regex.Replace(sqlContent, @"\(date_format\((?<field>`[\w_ -]*`.`[\w_ -]*`),'%Y%m%d'\) collate \w*\)", @"date_format(${field},'%Y%m%d')", regOptions);
        sqlContent = Regex.Replace(sqlContent, @"\((?<func>(right|left))\((?<field>`[\w_ -]*`.`[\w_ -]*`),(?<n>[0-9]*)\) collate \w*", @"${func}(${field},${n})", regOptions);
        sqlContent = Regex.Replace(sqlContent, @"\(mid\((?<field>`[\w_ -]*`.`[\w_ -]*`),(?<b>[0-9]*),(?<e>[0-9]*)\) collate \w*", @"mid(${field},${b},${e})", regOptions);
        sqlContent = Regex.Replace(sqlContent, @"--;", "-- ");
        sqlContent = Regex.Replace(sqlContent, @"(\s*;+\s*;+)+", $";{Environment.NewLine}");
        sqlContent = Regex.Replace(sqlContent, @"(;(\s)*;(\s)*;+)+", $";{Environment.NewLine}{Environment.NewLine}");
        sqlContent = Regex.Replace(sqlContent, @"\r\n;\r\n", string.Empty);
        sqlContent = Regex.Replace(sqlContent, @"(\r\n){2,}", Environment.NewLine);
        return sqlContent;
    }
}