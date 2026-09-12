# Regira DAL — SQL Server

Regira DAL.SqlServer provides SQL Server backup/restore through the server's own `BACKUP DATABASE` / `RESTORE DATABASE` (settings, options, and backup/restore services — no CRUD or communicator surface). No client tools are needed: both statements run over an ordinary connection.

## Projects

| Project | Package | Backend | CRUD | Backup / Restore |
|---------|---------|---------|------|-----------------|
| `DAL.SqlServer` | `Regira.DAL.SqlServer` | SQL Server | — | ✓ (native `.bak`) |

## Installation

```xml
<PackageReference Include="Regira.DAL.SqlServer" Version="6.*" />
```

---

## SqlServerSettings

| Property | Type | Description |
|----------|------|-------------|
| `Host` | `string` | Server name, `host\instance` or `(localdb)\MSSQLLocalDB`; default `"localhost"` |
| `DatabaseName` | `string?` | Target database |
| `Username` | `string?` | SQL login; `null` connects with Windows authentication |
| `Password` | `string?` | SQL login password |
| `Port` | `string` | Default `"1433"`; any other port is appended as `host,port` |
| `UseSecure` | `bool` | Require an encrypted connection; default `false` |
| `TrustServerCertificate` | `bool` | Accept the server's certificate without validating it |

```csharp
var settings = new SqlServerSettings("localhost", "shop", "sa", "pass");
string cs    = settings.BuildConnectionString();
```

For any other authentication mode (Microsoft Entra ID, a managed identity), set `SqlServerOptions.ConnectionString` instead of `DbSettings`.

## SqlServerBackupService / SqlServerRestoreService

Both work on the database named in the connection (`Database` / `Initial Catalog`).

```csharp
var options = new SqlServerOptions
{
    DbSettings      = new SqlServerSettings("localhost", "shop", "sa", "pass"),
    BackupDirectory = @"D:\SqlBackups",
    Overwrite       = true
};

// second parameter is an optional ILogger
IMemoryFile backup = await new SqlServerBackupService(options).Backup();
await new SqlServerRestoreService(options).Restore(backup);
```

### The backup directory

SQL Server writes and reads the `.bak` file itself: on its own file system, under its own service account. `BackupDirectory` is therefore required, and it must be reachable from both sides — SQL Server's service account and this process each need read and write access.

`BackupDirectory` is the path as SQL Server sees it. `LocalBackupDirectory` is the same directory as this process sees it, and defaults to `BackupDirectory`, which is right when both run on one machine.

| SQL Server runs | `BackupDirectory` | `LocalBackupDirectory` |
|-----------------|-------------------|------------------------|
| On this machine | `D:\SqlBackups` | — |
| On another machine, folder shared | `D:\SqlBackups` | `\\db01\SqlBackups` |
| In a Linux container (`-v C:\sqlbackups:/var/opt/mssql/backup`) | `/var/opt/mssql/backup` | `C:\sqlbackups` |

The instance's own backup folder (`MSSQL\Backup`) usually admits only the service account and administrators, so an application can rarely read it. Every call uses a new file and deletes it afterwards; a file this process cannot delete is left in place with a logged warning.

### Backup

`Backup()` takes a copy-only full backup, so the differential base and log chain of a scheduled backup plan are left untouched. The returned file holds the whole `.bak` in memory.

### Restore

`Restore(file)` connects through `master` and creates the target database from the backup:

- An existing target database throws, unless `Overwrite = true`: then it is dropped, rolling back its open sessions. That happens only after SQL Server has read the backup's file list, so a file it cannot open never costs you the existing database.
- The data and log files go to the server's default data and log directories, named after the target database (`shop_staging.mdf`, `shop_staging_log.ldf`), so a backup restores under a new name beside its source.

`Exists` checks for a database on an open connection:

```csharp no-compile
bool exists = await restoreService.Exists(connection, "shop_staging");
```

Neither service joins an ambient `TransactionScope`: SQL Server refuses to back up or restore inside a transaction. Backup needs `db_backupoperator` (or `db_owner`) on the database; restore needs `dbcreator`.

## Backup/Restore contracts

Both services implement the shared contracts from [Common](https://regira.github.io/Regira-Packages/src/Common#dal-abstractions):

```csharp
public interface IDbBackupService  { Task<IMemoryFile> Backup(); }
public interface IDbRestoreService { Task Restore(IMemoryFile file); }
```

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/DAL.SqlServer/)** — Settings, the backup directory, backup and restore
1. [Examples](https://regira.github.io/Regira-Packages/src/DAL.SqlServer/docs/examples.html) — Copy a database under a new name, archive a backup and restore it later

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
