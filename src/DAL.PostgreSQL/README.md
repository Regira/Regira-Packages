# Regira DAL — PostgreSQL

Regira DAL.PostgreSQL provides PostgreSQL backup/restore via `pg_dump`/`pg_restore` (settings, options, and backup/restore services — no CRUD or communicator surface).

## Projects

| Project | Package | Backend | CRUD | Backup / Restore |
|---------|---------|---------|------|-----------------|
| `DAL.PostgreSQL` | `Regira.DAL.PostgreSQL` | PostgreSQL | — | ✓ (pg_dump) |

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
var settings = new PgSettings("localhost", "mydb", "postgres", "pass");
IProcessHelper processHelper = new ProcessHelper();   // Regira.System

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

The password reaches `pg_dump` / `pg_restore` through the process environment (`PGPASSWORD`), never through
the command line — `ProcessHelper` sets it on the process itself. A custom `IProcessHelper` that does not
override the environment overload of `ExecuteCommand` still authenticates, but by setting the variable from
the command, so the password lands wherever that implementation writes it.

## PgOptions

| Property | Type | Description |
|----------|------|-------------|
| `DbSettings` | `PgSettings?` | Connection details, or use `ConnectionString` |
| `ConnectionString` | `string?` | Alternative to `DbSettings` |
| `ToolsDirectory` | `string` | Where `pg_dump` / `pg_restore` live |
| `BackupSchemas` | `ICollection<string>?` | Backup these schemas only |
| `Overwrite` | `bool` | Replace the target database if it exists |
| `MaintenanceDatabase` | `string?` | Database to create/drop from, default `postgres` |

`Restore` creates the target database itself, connecting to `MaintenanceDatabase` to do so —
`CREATE DATABASE` cannot run from a connection to the database it creates. When the target already
exists, `Overwrite` drops and recreates it (so anything the backup does not contain is lost) and
without `Overwrite` the restore fails. PostgreSQL refuses to drop a database while other sessions are
connected to it.

The three database operations are also available on their own, against a connection to any *other*
database on the same server:

```csharp no-compile
bool exists = await pgRestore.Exists(connection, "staging-db");
await pgRestore.Drop(connection, "staging-db");
await pgRestore.Create(connection, "staging-db");
```

## BackupRestoreManager

Standalone manager — useful when you want both backup and restore from the same object.

```csharp
var settings = new PgSettings("localhost", "mydb", "postgres", "pass");
var options  = new PgOptions { DbSettings = settings, ToolsDirectory = "/usr/lib/postgresql/16/bin" };
IProcessHelper processHelper = new ProcessHelper();   // Regira.System

var mgr = new BackupRestoreManager(processHelper, options);
mgr.Backup(settings, "source-db", "/backups/snapshot.dump");   // synchronous (void)
// overwrite: drops target-db and recreates it from the backup
await mgr.Restore(settings, "target-db", "/backups/snapshot.dump", overwrite: true);
```

## Backup/Restore contracts

Both services implement the shared contracts from [Common](https://regira.github.io/Regira-Packages/src/Common#dal-abstractions):

```csharp
public interface IDbBackupService  { Task<IMemoryFile> Backup(); }
public interface IDbRestoreService { Task Restore(IMemoryFile file); }
```

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/DAL.PostgreSQL/)** — Settings, backup/restore, and BackupRestoreManager
1. [Examples](https://regira.github.io/Regira-Packages/src/DAL.PostgreSQL/docs/examples.html) — Schema-specific backup, copying a database, managing it yourself

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
