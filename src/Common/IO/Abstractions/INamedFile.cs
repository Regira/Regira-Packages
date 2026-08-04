namespace Regira.IO.Abstractions;

/// <summary>
/// Represents a named file abstraction that extends the capabilities of a memory-based file.
/// </summary>
public interface INamedFile : IMemoryFile
{
    /// <summary>
    /// The name of a file without folder information
    /// </summary>
    string? FileName { get; set; }
}