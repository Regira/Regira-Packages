using System.Data.Common;
using System.Globalization;
using Npgsql;
using Regira.DAL.Models;

namespace Regira.DAL.PostgreSQL.Core;

/// <summary>
/// A PostgreSQL connection, read from and written to a connection string by Npgsql's own
/// <see cref="NpgsqlConnectionStringBuilder"/>, so a password holding <c>;</c>, <c>=</c> or quotes survives the round trip.
/// A port written with the host (<c>db01:5433</c>) is the port, unless one is given of its own.
/// </summary>
public class PgSettings : DbSettingsBase
{
    public PgSettings(string? host = null, string? databaseName = null, string? username = null, string? password = null, string? port = null)
        : base(host ?? PgDefaults.Host, databaseName, port ?? string.Empty, username ?? PgDefaults.Username, password ?? PgDefaults.Password, false)
    {
        CleanupHostAndPort();
        if (string.IsNullOrEmpty(Port))
        {
            Port = PgDefaults.Port;
        }
    }


    public static PgSettings FromConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new PgSettings(
            host: NullIfEmpty(builder.Host),
            databaseName: NullIfEmpty(builder.Database),
            username: NullIfEmpty(builder.Username),
            password: NullIfEmpty(builder.Password),
            // without a port of its own, a port written with the host is the port; Npgsql's own ContainsKey answers for
            // every keyword it knows, so the plain builder tells whether the string holds one
            port: new DbConnectionStringBuilder { ConnectionString = connectionString }.ContainsKey("Port")
                ? builder.Port.ToString(CultureInfo.InvariantCulture)
                : null
        );
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Username = Username,
            Password = Password
        };
        if (DatabaseName != null)
        {
            builder.Database = DatabaseName;
        }
        var port = int.Parse(Port, CultureInfo.InvariantCulture);
        if (port != NpgsqlConnection.DefaultPort)
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


    [Obsolete("PgSettings reads connection strings with NpgsqlConnectionStringBuilder. Will be removed in v7.")]
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
