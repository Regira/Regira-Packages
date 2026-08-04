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


    public static MongoSettings FromConnectionString(string connectionString)
    {
        var mongoUrl = MongoUrl.Create(connectionString);
        var host = mongoUrl.Url.Split(':').First();
        var port = mongoUrl.Url.Split(':').LastOrDefault() ?? MongoDefaults.Port;

        return new MongoSettings(host, mongoUrl.DatabaseName)
        {
            Host = host,
            DatabaseName = mongoUrl.DatabaseName,
            Username = mongoUrl.Username,
            Password = mongoUrl.Password,
            Port = port,
            UseSecure = mongoUrl.UseTls
        };
    }
    public override string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions)
    {
        //mongodb[+srv]://[username:password@]host[:port][/[database]]
        var connectionString = "mongodb";
        if (UseSecure)
        {
            connectionString += "+serv";
        }
        connectionString += "://";
        if (!string.IsNullOrEmpty(Username))
        {
            connectionString += $"{Username}:{Password}@";
        }
        connectionString += $"{Host}:{Port}";
        if (DatabaseName != null)
        {
            connectionString += $"/{DatabaseName};";
        }

        return connectionString;
    }
    public override T Clone<T>()
    {
        var cn = BuildConnectionString();
        return (FromConnectionString(cn) as T)!;
    }
}