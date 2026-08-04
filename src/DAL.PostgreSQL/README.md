# Regira DAL — PostgreSQL

Regira DAL.PostgreSQL provides PostgreSQL connectivity via Dapper, plus backup/restore via `pg_dump`/`pg_restore`.

## Projects

| Project | Package | Backend | CRUD | Backup / Restore |
|---------|---------|---------|------|-----------------|
| `DAL.PostgreSQL` | `Regira.DAL.PostgreSQL` | PostgreSQL | via Dapper | ✓ (pg_dump) |

## Installation

```xml
<PackageReference Include="Regira.DAL.PostgreSQL" Version="6.*" />
```

---

## PgSettings

| Property | Type | Description |
|----------|------|-------------|
| `Host` | `string` | Hostname |
| `DatabaseName` | `string` | Target database |
| `Username` | `string?` | Auth username |
| `Password` | `string?` | Auth password |
| `Port` | `string` | Default `"5432"` |

```csharp
var settings = new PgSettings("localhost", "mydb", "postgres", "pass");
```

## PgBackupService / PgRestoreService

Requires `pg_dump` / `pg_restore` executables. Supports schema-specific backups.

```csharp
var options = new PgOptions
{
    DbSettings     = settings,
    ToolsDirectory = "/usr/lib/postgresql/16/bin",
    BackupSchemas  = ["public", "reports"],
    Overwrite      = true
};

IMemoryFile backup = await new PgBackupService(options, processHelper).Backup();
await new PgRestoreService(options, processHelper).Restore(backup);
```

`PgRestoreService` can also create the target database if it does not exist:

```csharp
await pgRestore.Create(connection, "new_database");
bool exists = await pgRestore.Exists(connection, "new_database");
```

## BackupRestoreManager

Standalone manager — useful when you want both backup and restore from the same object.

```csharp
var mgr = new BackupRestoreManager(processHelper, options);
await mgr.Backup(settings, "sourceDb", "/backups/snapshot.dump");
await mgr.Restore(settings, "targetDb", "/backups/snapshot.dump", overwrite: true);
```

## Backup/Restore contracts

Both services implement the shared contracts from [Common](https://regira.github.io/Regira-Packages/src/Common#dal-abstractions):

```csharp
public interface IDbBackupService  { Task<IMemoryFile> Backup(); }
public interface IDbRestoreService { Task Restore(IMemoryFile file); }
```

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/DAL.PostgreSQL/)** — Settings, backup/restore, and BackupRestoreManager
1. [Examples](https://regira.github.io/Regira-Packages/src/DAL.PostgreSQL/docs/examples.html) — Schema-specific backup, create and restore
