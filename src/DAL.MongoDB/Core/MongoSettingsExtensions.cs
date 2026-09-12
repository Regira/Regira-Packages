using MongoDB.Driver;

namespace Regira.DAL.MongoDB.Core;

public static class MongoSettingsExtensions
{
    /// <summary>
    /// Driver settings built from the same URI the backup services hand to the tools, so the scheme, every
    /// replica-set member, TLS and the credentials all reach the driver by one route.
    /// </summary>
    /// <remarks>
    /// Composing <see cref="MongoClientSettings"/> member by member cannot express what a URI can: a single
    /// <c>Server</c> and port is no way to say "resolve this DNS seedlist" (<c>mongodb+srv://</c>, where the seed host
    /// itself need not accept connections) or "these three replica-set members". Parsing the URI is what keeps this
    /// path and <see cref="MongoSettings.BuildConnectionString(bool, KeyValuePair{string, string}[])"/> from
    /// disagreeing about the same deployment.
    /// </remarks>
    public static MongoClientSettings ToMongoClientSettings(this MongoSettings settings)
        => MongoClientSettings.FromConnectionString(settings.BuildConnectionString());

    /// <summary>
    /// The database the credentials are verified against, applying MongoDB's own default when
    /// <see cref="MongoSettings.AuthenticationDatabase"/> is not set: the connection's database, or <c>admin</c>.
    /// </summary>
    public static string ResolveAuthenticationDatabase(this MongoSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.AuthenticationDatabase))
        {
            return settings.AuthenticationDatabase!;
        }

        return string.IsNullOrEmpty(settings.DatabaseName) ? "admin" : settings.DatabaseName!;
    }
}
