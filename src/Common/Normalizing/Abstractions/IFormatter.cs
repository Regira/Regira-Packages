namespace Regira.Normalizing.Abstractions;

public interface IFormatter
{
    string? Normalize(string? input);
}