# Regira DAL.PostgreSQL — Examples

## Example 1: Schema-specific backup

```csharp
var options = new PgOptions
{
    DbSettings     = new PgSettings("pg.example.com", "prod-db", "postgres", "pass"),
    ToolsDirectory = "/usr/lib/postgresql/16/bin",
    BackupSchemas  = ["public"]
};

var processHelper = new ProcessHelper();  // or inject IProcessHelper

IMemoryFile backup = await new PgBackupService(options, processHelper).Backup();
```

---

## Example 2: Copy one database onto another

`Overwrite` drops the target database and recreates it from the backup — anything the backup does not
contain is lost. Without it, restoring onto a database that already exists fails.

```csharp
var processHelper = new ProcessHelper();

var source = new PgOptions
{
    DbSettings     = new PgSettings("pg.example.com", "prod-db", "postgres", "pass"),
    ToolsDirectory = "/usr/lib/postgresql/16/bin"
};
var target = new PgOptions
{
    DbSettings     = new PgSettings("pg.example.com", "staging-db", "postgres", "pass"),
    ToolsDirectory = "/usr/lib/postgresql/16/bin",
    Overwrite      = true
};

IMemoryFile backup = await new PgBackupService(source, processHelper).Backup();
await new PgRestoreService(target, processHelper).Restore(backup);
```

---

## Example 3: Managing the target database yourself

`Restore` creates the target database itself, from a connection to the maintenance database — `postgres`
unless `MaintenanceDatabase` says otherwise, since `CREATE DATABASE` cannot run from a connection to the
database it creates. The same three operations are available separately, against any connection to a
*different* database on the same server:

```csharp
var options = new PgOptions
{
    DbSettings     = new PgSettings("pg.example.com", "staging-db", "postgres", "pass"),
    ToolsDirectory = "/usr/lib/postgresql/16/bin"
};
var restorer = new PgRestoreService(options, new ProcessHelper());

await using var maintenance = new NpgsqlConnection("Server=pg.example.com;Database=postgres;User ID=postgres;Password=pass;");
await maintenance.OpenAsync();

if (await restorer.Exists(maintenance, "staging-db"))
{
    // PostgreSQL refuses this while other sessions are connected to staging-db
    await restorer.Drop(maintenance, "staging-db");
}

await restorer.Create(maintenance, "staging-db");
```

---

## Overview

1. [Index](../README.md) — Settings, backup/restore, and BackupRestoreManager
1. **[Examples](examples.md)** — Schema-specific backup, copying a database, managing it yourself
