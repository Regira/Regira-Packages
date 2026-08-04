using Regira.DAL.Models;

namespace Regira.DAL.PostgreSQL.Core;

public class PgSettings : DbSettingsBase
{
    public PgSettings(string? host = null, string? databaseName = null, string? username = null, string? password = null, string? port = null)
        : base(host ?? PgDefaults.Host, databaseName, port ?? PgDefaults.Port, username ?? PgDefaults.Username, password ?? PgDefaults.Password, false)
    {
        CleanupHostAndPort();
    }


    public static PgSettings FromConnectionString(string connectionString)
    {
        var dic = connectionString.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2 && !string.IsNullOrEmpty(p[1]))
            .Select(p => new KeyValuePair<string, string>(p[0], p[1]))
            .ToDictionary(k => k.Key, v => v.Value, StringComparer.CurrentCultureIgnoreCase);
        return new PgSettings(
            host: GetConnectionStringValue(dic, "server") ?? GetConnectionStringValue(dic, "host"),
            databaseName: GetConnectionStringValue(dic, "database"),
            username: GetConnectionStringValue(dic, "user id") ?? GetConnectionStringValue(dic, "uid") ?? GetConnectionStringValue(dic, "username"),
            password: GetConnectionStringValue(dic, "password") ?? GetConnectionStringValue(dic, "pwd"),
            port: GetConnectionStringValue(dic, "port")
        );
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
    {
        var connectionString = $"Server={Host};User ID={Username};Password={Password};";
        if (DatabaseName != null)
        {
            connectionString += $"Database={DatabaseName};";
        }
        if (Port != PgDefaults.Port)
        {
            connectionString += $"Port={Port};";
        }

        if (extraOptions.Any())
        {
            connectionString += string.Join(";", extraOptions.Select(x => $"{x.Key}={x.Value}"));
        }

        return connectionString;
    }
    public override T Clone<T>()
    {
        var cn = BuildConnectionString();
        return (FromConnectionString(cn) as T)!;
    }
    public void CleanupHostAndPort()
    {
        // clean up host & port
        if (Host.Contains(':'))
        {
            var hostSegments = Host.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (hostSegments.Length == 2)
            {
                Host = hostSegments.First();
                if (string.IsNullOrEmpty(Port))
                {
                    var portFromHost = hostSegments.Last();
                    Port = portFromHost;
                }
            }
        }
    }


    protected static string? GetConnectionStringValue(IDictionary<string, string> cnDic, string key)
    {
        if (cnDic.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }
}