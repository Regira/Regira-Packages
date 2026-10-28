namespace Regira.IO.Storage;

/// <summary>What a storage backend knows of a stored file without reading its content.</summary>
/// <param name="Length">The size in bytes.</param>
/// <param name="LastModified">When the content was last written, in UTC; <c>null</c> where the backend keeps no such time (GitHub).</param>
public record StoredFileInfo(long Length, DateTime? LastModified);
