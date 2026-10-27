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
| `Password` | `string?` | Auth password. Left out, here or in a connection string, it is `PgDefaults.Password` (`"postgres"`), which the tools get as `PGPASSWORD` and which therefore wins over a `.pgpass` file |
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

Both services start `pg_dump` / `pg_restore` directly, without a shell in between, so they run on Windows,
Linux and macOS alike, and a database, user or schema name reaches the tool exactly as written. An
`IProcessHelper` of your own sees `ExecuteFile` rather than `ExecuteCommand`.

The password reaches the tools through the process environment (`PGPASSWORD`), never through the arguments —
`ProcessHelper` sets it on the process itself. A custom `IProcessHelper` has to override the environment
overload of `ExecuteFile` to pass it on; the default implementation throws `NotSupportedException` rather than
start the tool without it.

## PgOptions

| Property | Type | Description |
|----------|------|-------------|
| `DbSettings` | `PgSettings?` | Connection details, or use `ConnectionString` |
| `ConnectionString` | `string?` | Alternative to `DbSettings` |
| `ToolsDirectory` | `string` | Where `pg_dump` / `pg_restore` live |
| `BackupSchemas` | `ICollection<string>?` | Backup these schemas only — each name matched exactly, case and all; a schema that does not exist fails the backup |
| `Overwrite` | `bool` | Replace the target database if it exists |
| `MaintenanceDatabase` | `string?` | Database to create/drop from, default `postgres` |

`Restore` creates the target database itself, connecting to `MaintenanceDatabase` to do so —
`CREATE DATABASE` cannot run from a connection to the database it creates. When the target already
exists, `Overwrite` drops and recreates it (so anything the backup does not contain is lost) and
without `Overwrite` the restore fails. The maintenance connection never joins an ambient `TransactionScope`,
inside which PostgreSQL refuses to create or drop a database.

`Overwrite` onto an existing database needs PostgreSQL 13 or later and a role that may create databases
(`CREATEDB`) and end the sessions on the target (superuser, a member of their role, or `pg_signal_backend`);
otherwise the restore fails before anything is dropped. It ends those sessions (`DROP DATABASE … WITH (FORCE)`)
and clears the Npgsql pools of connections this process opened from a connection string, so an app can restore its
own database while it runs:

- a query running on the target at that moment fails;
- a pool the clear does not reach, an `NpgsqlDataSource` of the app's own (`AddNpgsqlDataSource`, or EF Core's
  `UseNpgsql` with `MapEnum` or a plugin) or another process, fails its next query once, unless you call its
  `Clear()` after the restore;
- until `pg_restore` finishes, the app sees the database missing, then partly restored, so keep its writes out of
  that window; a failure after the drop leaves the database missing or partly restored.

The three database operations are also available on their own, against a connection to any *other*
database on the same server. `Drop` ends no session: PostgreSQL refuses it while other sessions are connected.

<!-- no-compile -->
```csharp
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
