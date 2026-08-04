namespace Regira.DAL.MongoDB.Constants;

public static class MongoDefaults
{
    public static string Host { get; set; } = "localhost";
    public static string Port { get; set; } = "27017";
    public static bool UseTls { get; set; } = false;
}