using Microsoft.Data.SqlClient;
using Regira.DAL.Models;

namespace Regira.DAL.SqlServer.Core;

public class SqlServerSettings : DbSettingsBase
{
    private const string DefaultPort = "1433";

    public SqlServerSettings() : this(host: null)
    {
    }
    /// <param name="host">Server name, <c>host\instance</c> or <c>(localdb)\MSSQLLocalDB</c></param>
    /// <param name="databaseName"></param>
    /// <param name="username">SQL login; <c>null</c> connects with Windows authentication</param>
    /// <param name="password"></param>
    /// <param name="port"></param>
    public SqlServerSettings(string? host = null, string? databaseName = null, string? username = null, string? password = null, string? port = null)
        : base(host ?? SqlServerDefaults.Host, databaseName, port ?? SqlServerDefaults.Port, username, password, false)
    {
    }

    /// <summary>
    /// Accept the server's certificate without validating it, e.g. the self-signed certificate of a development server
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// <c>Encrypt=Strict</c>: TLS before the login (TDS 8.0, SQL Server 2022 and later), rather than the
    /// <c>Mandatory</c> encryption <see cref="Regira.DAL.Models.DbSettingsBase.UseSecure"/> asks for.
    /// </summary>
    public bool UseStrictEncryption { get; set; }


    public static SqlServerSettings FromConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        // SQL Server separates the port with a comma: "host,1444" or "host\instance,1444"
        var portIndex = builder.DataSource.LastIndexOf(',');
        var host = portIndex < 0 ? builder.DataSource : builder.DataSource[..portIndex];
        var port = portIndex < 0 ? null : builder.DataSource[(portIndex + 1)..];
        var useSqlLogin = !builder.IntegratedSecurity && !string.IsNullOrEmpty(builder.UserID);

        return new SqlServerSettings(
            host: string.IsNullOrEmpty(host) ? null : host,
            databaseName: string.IsNullOrEmpty(builder.InitialCatalog) ? null : builder.InitialCatalog,
            username: useSqlLogin ? builder.UserID : null,
            password: useSqlLogin ? builder.Password : null,
            port: port
        )
        {
            UseSecure = !builder.Encrypt.Equals(SqlConnectionEncryptOption.Optional),
            UseStrictEncryption = builder.Encrypt.Equals(SqlConnectionEncryptOption.Strict),
            TrustServerCertificate = builder.TrustServerCertificate
        };
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrEmpty(Port) || Port == DefaultPort ? Host : $"{Host},{Port}",
            Encrypt = UseStrictEncryption ? SqlConnectionEncryptOption.Strict
                : UseSecure ? SqlConnectionEncryptOption.Mandatory
                : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = TrustServerCertificate
        };
        if (!string.IsNullOrEmpty(DatabaseName))
        {
            builder.InitialCatalog = DatabaseName;
        }
        if (string.IsNullOrEmpty(Username))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = Username;
            builder.Password = Password ?? string.Empty;
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
}
