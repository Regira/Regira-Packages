# Regira DAL.MySQL — Examples

## Example 1: Connect and query with Dapper

```csharp
var settings = new MySqlSettings("localhost", "mydb", username: "root", password: "pass");
var comm     = new MySqlCommunicator(settings.BuildConnectionString());

await comm.Open();
var orders = await comm.DbConnection.QueryAsync<Order>(
    "SELECT * FROM orders WHERE status = @status",
    new { status = "pending" });
await comm.Close();
```

---

## Example 2: Backup and restore

```csharp
var options = new MySqlBackupOptions { DbSettings = settings };

// Backup
IMemoryFile backup = await new MySqlBackupService(options).Backup();
await fileService.Save("backups/mysql-latest.sql.gz", backup.Bytes!);

// Restore to a different database
var restoreOptions = new MySqlBackupOptions
{
    DbSettings = new MySqlSettings("localhost", "mydb-staging", username: "root", password: "pass")
};
IMemoryFile snapshot = (await fileService.GetBytes("backups/mysql-latest.sql.gz"))!
                            .ToBinaryFile("snapshot.sql.gz");
await new MySqlRestoreService(restoreOptions).Restore(snapshot);
```

---

## Overview

1. [Index](../README.md) — Settings, communicator, backup/restore
1. **[Examples](examples.md)** — Connect, query, backup, and restore
