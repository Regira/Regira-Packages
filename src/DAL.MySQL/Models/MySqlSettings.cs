using System.Globalization;
using MySqlConnector;
using Regira.DAL.Models;

namespace Regira.DAL.MySQL.Models;

/// <summary>
/// A MySQL connection, read from and written to a connection string by MySqlConnector's own
/// <see cref="MySqlConnectionStringBuilder"/>, so a password holding <c>;</c>, <c>=</c> or quotes survives the round
/// trip. A port written with the host (<c>db01:3307</c>) is the port, unless one is given of its own.
/// </summary>
public class MySqlSettings : DbSettingsBase
{
    private const uint DefaultPort = 3306;

    public MySqlSettings() : this(null)
    {
    }
    public MySqlSettings(string? host, string? databaseName = null, string? port = null, string? username = null, string? password = null)
        : base(host ?? MySqlDefaults.Host, databaseName, port ?? string.Empty, username ?? MySqlDefaults.Username, password ?? MySqlDefaults.Password, false)
    {
        CleanupHostAndPort();
        if (string.IsNullOrEmpty(Port))
        {
            Port = MySqlDefaults.Port;
        }
    }


    public static MySqlSettings FromConnectionString(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        return new MySqlSettings(
            host: NullIfEmpty(builder.Server),
            databaseName: NullIfEmpty(builder.Database),
            username: NullIfEmpty(builder.UserID),
            password: NullIfEmpty(builder.Password),
            // without a port of its own, a port written with the host is the port
            port: builder.ContainsKey("Port") ? builder.Port.ToString(CultureInfo.InvariantCulture) : null
        );
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = Host,
            UserID = Username,
            Password = Password
        };
        if (DatabaseName != null)
        {
            builder.Database = DatabaseName;
        }
        var port = uint.Parse(Port, CultureInfo.InvariantCulture);
        if (port != DefaultPort)
        {
            builder.Port = port;
        }

        foreach (var option in extraOptions)
        {
            builder[option.Key] = option.Value;
        }

        return builder.ConnectionString;
    }
    public override T Clone<T>()
    {
        var cn = BuildConnectionString();
        return (FromConnectionString(cn) as T)!;
    }
    /// <summary>
    /// Splits a host written as <c>host:port</c>, the port taken unless one is given of its own. A host list (a comma)
    /// or an IPv6 address (more than one colon) stays as it is.
    /// </summary>
    public void CleanupHostAndPort()
    {
        var colon = Host.IndexOf(':');
        if (colon <= 0 || colon != Host.LastIndexOf(':') || Host.Contains(',')
            || !uint.TryParse(Host[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return;
        }
        if (string.IsNullOrEmpty(Port))
        {
            Port = Host[(colon + 1)..];
        }
        Host = Host[..colon];
    }


    [Obsolete("MySqlSettings reads connection strings with MySqlConnectionStringBuilder. Will be removed in v7.")]
    protected static string? GetConnectionStringValue(IDictionary<string, string> cnDic, string key)
    {
        if (cnDic.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
