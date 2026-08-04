using Regira.IO.Abstractions;

namespace Regira.IO.Models;

/// <summary>
/// Represents a text file item that extends the functionality of a binary file item 
/// by including support for text content.
/// </summary>
public class TextFileItem : BinaryFileItem, ITextFile
{
    /// <summary>
    /// Gets or sets the textual content of the file.
    /// </summary>
    /// <remarks>
    /// This property allows access to the text-based content of the file, 
    /// extending the functionality of the binary file representation.
    /// </remarks>
    public string? Contents { get; set; }
}