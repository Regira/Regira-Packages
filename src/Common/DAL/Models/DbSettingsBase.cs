using Regira.DAL.Abstractions;

namespace Regira.DAL.Models;

/// <summary>
/// Represents the base class for database settings, providing common properties and methods 
/// for configuring and managing database connections.
/// </summary>
/// <remarks>
/// This abstract class is designed to be inherited by specific database settings implementations.
/// It provides properties such as <see cref="Host"/>, <see cref="DatabaseName"/>, <see cref="Port"/>, 
/// <see cref="Username"/>, <see cref="Password"/>, and <see cref="UseSecure"/> to define the 
/// connection details. Additionally, it includes methods for building connection strings, 
/// comparing connection strings, and cloning settings.
/// </remarks>
public abstract class DbSettingsBase(string host, string? databaseName, string port, string? username = null, string? password = null, bool useSecure = true)
    : IDbSettings
{
    public string Host { get; set; } = host;
    public string? DatabaseName { get; set; } = databaseName;
    public string Port { get; set; } = port;
    public string? Username { get; set; } = username;
    public string? Password { get; set; } = password;
    public bool UseSecure { get; set; } = useSecure;


    public abstract string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions);
    public virtual bool EqualsConnectionString<T>(T other)
        where T : class, IDbSettings, new()
    {
        return BuildConnectionString().Equals(other.BuildConnectionString());
    }

    public abstract T Clone<T>()
        where T : class, IDbSettings, new();
}