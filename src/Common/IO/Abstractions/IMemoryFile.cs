namespace Regira.IO.Abstractions;

/// <summary>
/// Defines an abstraction for a memory-based file that supports both byte and stream representations.
/// </summary>
public interface IMemoryFile : IMemoryBytesFile, IMemoryStreamFile
{
    new string? ContentType { get; set; }
    new long Length { get; set; }
}

/// <summary>
/// Represents an abstraction for a memory-based file that provides access to its content as a byte array.
/// </summary>
public interface IMemoryBytesFile
{
    string? ContentType { get; set; }
    long Length { get; set; }
    byte[]? Bytes { get; set; }
}
/// <summary>
/// Represents an abstraction for a memory-based file that provides access to its content as a stream.
/// </summary>
public interface IMemoryStreamFile : IDisposable
{
    string? ContentType { get; set; }
    long Length { get; set; }
    Stream? Stream { get; set; }
}