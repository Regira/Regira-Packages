using Regira.DAL.Models;

namespace Regira.DAL.MySQL.Models;

public class MySqlSettings : DbSettingsBase
{
    public MySqlSettings() : this(null)
    {
    }
    public MySqlSettings(string? host, string? databaseName = null, string? port = null, string? username = null, string? password = null)
        : base(host ?? MySqlDefaults.Host, databaseName, port ?? MySqlDefaults.Port, username ?? MySqlDefaults.Username, password ?? MySqlDefaults.Password, false)
    {
        CleanupHostAndPort();
    }


    public static MySqlSettings FromConnectionString(string connectionString)
    {
        var dic = connectionString.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2 && !string.IsNullOrEmpty(p[1]))
            .Select(p => new KeyValuePair<string, string>(p[0], p[1]))
            .ToDictionary(k => k.Key, v => v.Value, StringComparer.CurrentCultureIgnoreCase);

        var host = GetConnectionStringValue(dic, "server") ?? GetConnectionStringValue(dic, "host");
        var databaseName = GetConnectionStringValue(dic, "database");
        var username = GetConnectionStringValue(dic, "user id") ?? GetConnectionStringValue(dic, "uid") ?? GetConnectionStringValue(dic, "username");
        var password = GetConnectionStringValue(dic, "password") ?? GetConnectionStringValue(dic, "pwd");
        var port = GetConnectionStringValue(dic, "port");

        return new MySqlSettings(
            host: host,
            databaseName: databaseName,
            username: username,
            password: password,
            port: port
        );
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
    {
        //Server=127.0.0.1;Port=3306;Database=db_name;Uid=root;Pwd=;SslMode=none
        var connectionString = $"Server={Host};Uid={Username};Pwd={Password};";
        if (DatabaseName != null)
        {
            connectionString += $"Database={DatabaseName};";
        }
        if (Port != MySqlDefaults.Port)
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