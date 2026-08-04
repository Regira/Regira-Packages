using MongoDB.Driver;

namespace Regira.DAL.MongoDB.Core;

public static class MongoSettingsExtensions
{
    public static MongoClientSettings ToMongoClientSettings(this MongoSettings settings)
    {
        var mongoClientSettings = new MongoClientSettings
        {
            Server = new MongoServerAddress(settings.Host, int.Parse(settings.Port)),
            UseTls = settings.UseSecure,
            //UseSsl = settings.UseSsl
        };

        return mongoClientSettings;
    }
}