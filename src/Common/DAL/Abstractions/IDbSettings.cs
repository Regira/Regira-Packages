namespace Regira.DAL.Abstractions;

/// <summary>
/// Defines the contract for database settings, providing properties and methods 
/// to configure and manage database connections.
/// </summary>
/// <remarks>
/// This interface specifies the essential properties such as <see cref="Host"/>, 
/// <see cref="DatabaseName"/>, <see cref="Port"/>, <see cref="Username"/>, 
/// <see cref="Password"/>, and <see cref="UseSecure"/> required for database configuration. 
/// It also includes methods for building connection strings, comparing connection strings, 
/// and cloning settings.
/// </remarks>
public interface IDbSettings
{
    string Host { get; set; }
    string? DatabaseName { get; set; }
    string Port { get; set; }
    string? Username { get; set; }
    string? Password { get; set; }
    bool UseSecure { get; set; }

    string BuildConnectionString(params KeyValuePair<string, string>[] extraOptions);
    bool EqualsConnectionString<T>(T other)
        where T : class, IDbSettings, new();
    T Clone<T>()
        where T : class, IDbSettings, new();
}