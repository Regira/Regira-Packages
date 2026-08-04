# Regira DAL.PostgreSQL — Examples

## Example 1: Schema-specific backup

```csharp
var options = new PgOptions
{
    DbSettings     = new PgSettings("pg.example.com", "prod-db", "postgres", "pass"),
    ToolsDirectory = "/usr/lib/postgresql/16/bin",
    BackupSchemas  = ["public"],
    Overwrite      = true
};

var processHelper = new ProcessHelper();  // or inject IProcessHelper

IMemoryFile backup = await new PgBackupService(options, processHelper).Backup();
```

---

## Example 2: Create database then restore

```csharp
await using var conn = new NpgsqlConnection(adminConnectionString);
await conn.OpenAsync();

var restorer = new PgRestoreService(options, processHelper);

if (!await restorer.Exists(conn, "staging-db"))
    await restorer.Create(conn, "staging-db");

await restorer.Restore(backup);
```

---

## Overview

1. [Index](../README.md) — Settings, backup/restore, and BackupRestoreManager
1. **[Examples](examples.md)** — Schema-specific backup, create and restore
