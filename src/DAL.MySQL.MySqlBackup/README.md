# Regira.DAL.MySQL.MySqlBackup

Backup and restore for [Regira DAL.MySQL](https://regira.github.io/Regira-Packages/src/DAL.MySQL/), built on [MySqlBackup.NET](https://www.nuget.org/packages/MySqlBackup.NET.MySqlConnector). `MySqlBackupService` implements `IDbBackupService` and exports a database to an `IMemoryFile`; `MySqlRestoreService` implements `IDbRestoreService` and imports one. Both work in memory, without temporary files, and take a `MySqlBackupOptions` holding a connection string or `MySqlSettings`.

## Installation

```xml
<PackageReference Include="Regira.DAL.MySQL.MySqlBackup" Version="6.*" />
```

## Documentation

- [Backup and restore](https://regira.github.io/Regira-Packages/src/DAL.MySQL/#mysqlbackupservice--mysqlrestoreservice) — configuring `MySqlBackupOptions`, taking a backup and restoring it
- [DAL.MySQL examples](https://regira.github.io/Regira-Packages/src/DAL.MySQL/docs/examples.html) — restoring a backup into a different database
- [Backup and restore contracts](https://regira.github.io/Regira-Packages/src/Common/#idbbackupservice--idbrestoreservice) — the shared interfaces, and the other databases that implement them

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
