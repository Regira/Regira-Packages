# Regira DAL — MySQL

Regira DAL.MySQL provides MySQL/MariaDB connectivity via Dapper, plus backup/restore via MySqlBackup.NET.

## Projects

| Project | Package | Backend | CRUD | Backup / Restore |
|---------|---------|---------|------|-----------------|
| `DAL.MySQL` | `Regira.DAL.MySQL` | MySQL / MariaDB | via Dapper | — |
| `DAL.MySQL.MySqlBackup` | `Regira.DAL.MySQL.MySqlBackup` | MySQL / MariaDB | — | ✓ (MySqlBackup.NET) |

## Installation

```xml
<PackageReference Include="Regira.DAL.MySQL"             Version="6.*" />
<PackageReference Include="Regira.DAL.MySQL.MySqlBackup" Version="6.*" />
```

---

## MySqlSettings

| Property | Type | Description |
|----------|------|-------------|
| `Host` | `string` | Hostname |
| `DatabaseName` | `string` | Target database |
| `Port` | `string` | Default `"3306"` |
| `Username` | `string?` | Auth username |
| `Password` | `string?` | Auth password |

```csharp
var settings = new MySqlSettings("localhost", "mydb", username: "root", password: "pass");
string cs    = settings.BuildConnectionString();
```

## MySqlCommunicator

Extends `DbCommunicator<MySqlConnection>` (Dapper). Execute raw queries via the underlying `DbConnection`.

```csharp no-compile
var comm = new MySqlCommunicator(settings.BuildConnectionString());
var rows = await comm.OpenDbConnection.QueryAsync<Product>("SELECT * FROM products");
```

## SQLDumpManager

Corrects query ordering in a SQL dump file to ensure foreign key constraints are satisfied during restoration.

```csharp
var settings = new MySqlSettings("localhost", "mydb", username: "root", password: "pass");
string sqlDumpContent = await File.ReadAllTextAsync("dump.sql");

var mgr = new SQLDumpManager(settings, null); // null splitter → SQLDumpManager.DefaultSplitter
mgr.OnAction += (msg, data) => Console.WriteLine(msg);

var result = await mgr.CorrectQuerySequence(sqlDumpContent);
// result.Output  — corrected SQL
// result.Failed  — queries that could not be ordered
```

## MySqlBackupService / MySqlRestoreService

Stream-based — no temp files.

```csharp
var settings = new MySqlSettings("localhost", "mydb", username: "root", password: "pass");
var options  = new MySqlBackupOptions { DbSettings = settings };

IMemoryFile backup = await new MySqlBackupService(options).Backup();
await new MySqlRestoreService(options).Restore(backup);
```

## Backup/Restore contracts

Both services implement the shared contracts from [Common](https://regira.github.io/Regira-Packages/src/Common#dal-abstractions):

```csharp
public interface IDbBackupService  { Task<IMemoryFile> Backup(); }
public interface IDbRestoreService { Task Restore(IMemoryFile file); }
```

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/DAL.MySQL/)** — Settings, communicator, backup/restore
1. [Examples](https://regira.github.io/Regira-Packages/src/DAL.MySQL/docs/examples.html) — Connect, query, backup, and restore

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
