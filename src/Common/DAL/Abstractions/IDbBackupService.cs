using Regira.IO.Abstractions;

namespace Regira.DAL.Abstractions;

/// <summary>
/// Provides an abstraction for database backup services, enabling the creation of backups
/// for various database implementations.
/// </summary>
public interface IDbBackupService
{
    Task<IMemoryFile> Backup();
}