using Regira.IO.Abstractions;

namespace Regira.DAL.Abstractions;

/// <summary>
/// Represents a service for restoring a database from a memory-based file.
/// </summary>
public interface IDbRestoreService
{
    Task Restore(IMemoryFile file);
}