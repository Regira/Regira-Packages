using MongoDB.Driver;
using Regira.DAL.Models;
using MongoDefaults = Regira.DAL.MongoDB.Constants.MongoDefaults;

namespace Regira.DAL.MongoDB.Core;

public class MongoSettings(
    string? host,
    string? database,
    string? port = null,
    string? username = null,
    string? password = null,
    bool? useTls = null)
    : DbSettingsBase(host ?? MongoDefaults.Host, database, port ?? MongoDefaults.Port, username, password,
        useTls ?? MongoDefaults.UseTls)
{
    public MongoSettings()
        : this(null, null)
    {
    }

    /// <summary>
    /// Database holding the user's credentials (<c>authSource</c>), when that is not <see cref="DbSettingsBase.DatabaseName"/>.
    /// Credentials are commonly created in <c>admin</c> while the application reads another database.
    /// </summary>
    /// <remarks>
    /// Left empty, MongoDB resolves the source itself: <see cref="DbSettingsBase.DatabaseName"/>, or <c>admin</c> when no database is named.
    /// </remarks>
    public string? AuthenticationDatabase { get; set; }

    /// <summary>
    /// The connection uses the <c>mongodb+srv://</c> scheme (Atlas and other DNS-seeded deployments), where DNS
    /// supplies the hosts and the port rather than the URI.
    /// </summary>
    /// <remarks>
    /// Read from the connection string and written back out again, so a URI survives
    /// <see cref="FromConnectionString"/> followed by <see cref="DbSettingsBase.BuildConnectionString"/> — which is
    /// what <see cref="Clone{T}"/> and the backup services do. Emitting <c>mongodb://</c> for an SRV deployment names
    /// one host that need not exist, and no port that DNS would have supplied.
    /// </remarks>
    public bool UseSrv { get; set; }

    /// <summary>
    /// Every other option of the connection string — <c>authMechanism</c>, <c>replicaSet</c>, <c>directConnection</c>,
    /// <c>readPreference</c>, <c>tlsCAFile</c>, … — in order, unescaped, a repeated one (<c>readPreferenceTags</c>)
    /// once per occurrence.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="FromConnectionString"/> and written back by
    /// <see cref="BuildConnectionString(bool, KeyValuePair{string, string}[])"/>, so the tools and the driver connect the
    /// way the connection string says — an X.509 login, a replica set reached through one member. <c>authSource</c> is
    /// not kept here, <see cref="AuthenticationDatabase"/> holds it, and neither is a <c>tls</c> that turns TLS on,
    /// which <see cref="DbSettingsBase.UseSecure"/> holds. A <c>tls=false</c> is kept — an SRV connection uses TLS
    /// unless told otherwise — and is left out while <see cref="DbSettingsBase.UseSecure"/> is on.
    /// </remarks>
    public IList<KeyValuePair<string, string>> UriOptions { get; } = new List<KeyValuePair<string, string>>();

    private static readonly HashSet<string> TlsOptions = new(StringComparer.OrdinalIgnoreCase) { "tls", "ssl" };


    public static MongoSettings FromConnectionString(string connectionString)
    {
        var mongoUrl = MongoUrl.Create(connectionString);
        var servers = mongoUrl.Servers?.ToList() ?? [];
        var server = servers.FirstOrDefault();
        var useSrv = connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase);

        // an SRV URI names a single DNS seed and no port; a replica set names every member, each with its own port
        var host = useSrv || servers.Count <= 1
            ? server?.Host
            : string.Join(",", servers.Select(x => $"{x.Host}:{x.Port}"));

        var settings = new MongoSettings(host, mongoUrl.DatabaseName)
        {
            Port = server != null ? server.Port.ToString() : MongoDefaults.Port,
            Username = mongoUrl.Username,
            Password = mongoUrl.Password,
            AuthenticationDatabase = mongoUrl.AuthenticationSource,
            UseSecure = mongoUrl.UseTls,
            UseSrv = useSrv
        };
        foreach (var (name, value) in ReadQuery(connectionString))
        {
            var modelled = name.Equals("authSource", StringComparison.OrdinalIgnoreCase)
                           || (TlsOptions.Contains(name) && !value.Equals("false", StringComparison.OrdinalIgnoreCase));
            if (!modelled)
            {
                settings.UriOptions.Add(new KeyValuePair<string, string>(name, value));
            }
        }
        return settings;
    }

    /// <summary>
    /// The URI's options in their order, unescaped. The connection string format allows <c>;</c> as a separator too.
    /// </summary>
    private static IEnumerable<(string Name, string Value)> ReadQuery(string connectionString)
    {
        var start = connectionString.IndexOf('?');
        if (start < 0)
        {
            yield break;
        }
        foreach (var option in connectionString[(start + 1)..].Split('&', ';'))
        {
            var separator = option.IndexOf('=');
            if (separator > 0)
            {
                yield return (Uri.UnescapeDataString(option[..separator]), Uri.UnescapeDataString(option[(separator + 1)..]));
            }
        }
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
        => BuildConnectionString(true, extraOptions);
    /// <summary>
    /// Builds the connection URI, with or without the password.
    /// </summary>
    /// <param name="includePassword">
    /// <c>false</c> leaves the password out and keeps the rest of the URI intact, for a consumer that passes the password
    /// through a channel of its own — a command line and a log are both readable by other processes.
    /// </param>
    /// <param name="extraOptions">Appended to the URI's query string after <see cref="UriOptions"/>, escaped</param>
    public string BuildConnectionString(bool includePassword, params KeyValuePair<string, string>[] extraOptions)
    {
        // mongodb://[username[:password]@]host[:port]/[database][?options]
        var connectionString = UseSrv ? "mongodb+srv://" : "mongodb://";
        if (!string.IsNullOrEmpty(Username))
        {
            connectionString += Uri.EscapeDataString(Username!);
            if (includePassword && !string.IsNullOrEmpty(Password))
            {
                connectionString += $":{Uri.EscapeDataString(Password!)}";
            }
            connectionString += "@";
        }
        // an SRV seed carries no port (DNS supplies it), and a replica-set host list carries a port per member
        connectionString += UseSrv || Host.Contains(',') ? $"{Host}/" : $"{Host}:{Port}/";
        if (!string.IsNullOrEmpty(DatabaseName))
        {
            connectionString += Uri.EscapeDataString(DatabaseName!);
        }

        var options = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrEmpty(AuthenticationDatabase))
        {
            options.Add(new KeyValuePair<string, string>("authSource", AuthenticationDatabase!));
        }
        if (UseSecure)
        {
            options.Add(new KeyValuePair<string, string>("tls", "true"));
        }
        options.AddRange(UriOptions.Where(option => !(UseSecure && TlsOptions.Contains(option.Key))));
        options.AddRange(extraOptions);
        if (options.Any())
        {
            connectionString += $"?{string.Join("&", options.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"))}";
        }

        return connectionString;
    }
    public override T Clone<T>()
    {
        var cn = BuildConnectionString();
        return (FromConnectionString(cn) as T)!;
    }
}
