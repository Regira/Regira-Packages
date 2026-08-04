namespace Regira.IO.Abstractions;

/// <summary>
/// Represents an abstraction for a text file that extends the capabilities of a binary file.
/// </summary>
public interface ITextFile : IBinaryFile
{
    string? Contents { get; set; }
}