namespace Regira.Serializing.Abstractions;

public interface ISerializer
{
    string Serialize<T>(T obj);
    T? Deserialize<T>(string? content);
    object? Deserialize(string? content, Type type);
}