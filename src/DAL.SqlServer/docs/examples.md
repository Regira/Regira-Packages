# Regira DAL.SqlServer — Examples

## Example 1: Copy a database under a new name

Back up `shop` and restore it as `shop_staging` on the same instance. The copy's files are named after `shop_staging`, so they never collide with the files `shop` still uses.

```csharp
var source = new SqlServerOptions
{
    ConnectionString     = "Server=db01;Database=shop;Integrated Security=true;TrustServerCertificate=true",
    BackupDirectory      = @"D:\SqlBackups",
    LocalBackupDirectory = @"\\db01\SqlBackups"
};
var target = new SqlServerOptions
{
    ConnectionString     = "Server=db01;Database=shop_staging;Integrated Security=true;TrustServerCertificate=true",
    BackupDirectory      = source.BackupDirectory,
    LocalBackupDirectory = source.LocalBackupDirectory,
    Overwrite            = true
};

IMemoryFile backup = await new SqlServerBackupService(source).Backup();
await new SqlServerRestoreService(target).Restore(backup);
```

---

## Example 2: Archive a backup, restore it later

```csharp
using Regira.IO.Extensions;

var options = new SqlServerOptions
{
    DbSettings      = new SqlServerSettings("localhost", "shop"),   // Windows authentication
    BackupDirectory = @"D:\SqlBackups"
};

IMemoryFile backup = await new SqlServerBackupService(options).Backup();
await File.WriteAllBytesAsync(@"E:\Archive\shop.bak", backup.GetBytes()!);

// later: restore the archived file over the current database
options.Overwrite = true;
var archived = await File.ReadAllBytesAsync(@"E:\Archive\shop.bak");
await new SqlServerRestoreService(options).Restore(archived.ToMemoryFile());
```

---

## Overview

1. [Index](../README.md) — Settings, the backup directory, backup and restore
1. **[Examples](examples.md)** — Copy a database under a new name, archive a backup and restore it later
