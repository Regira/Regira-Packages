namespace Regira.IO.Abstractions;

/// <summary>
/// Represents a storage file abstraction that extends the capabilities of a named file.
/// </summary>
public interface IStorageFile : INamedFile
{
    /// <summary>
    /// The folder structure, except the root folder
    /// </summary>
    string? Prefix { get; set; }
    /// <summary>
    /// Identifier in a specific context. Prefix + Filename
    /// </summary>
    string? Identifier { get; set; }

    /// <summary>
    /// The full path/Uri for this file
    /// </summary>
    string? Path { get; set; }
}