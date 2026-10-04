# DAL.SqlServer.Testing

NUnit tests for [DAL.SqlServer](../../src/DAL.SqlServer/README.md): settings and connection strings, server path
handling, options validation, and `BackupRestoreTests`, which backs up and restores real databases through
`BACKUP DATABASE` / `RESTORE DATABASE`.

## Running

```bash
dotnet test tests/DAL.SqlServer.Testing
```

`BackupRestoreTests` (category `LocalDb`) connects with `REGIRA_SQLSERVER_CONNECTION`, which the repository's
`Regira.runsettings` sets to `(localdb)\MSSQLLocalDB`. The login must be allowed to create databases; the fixture
creates `RegiraBackupTest_*` databases and drops them afterwards. It is skipped when the variable is empty, or when it
names a LocalDB instance and LocalDB is not installed. Backups go to a new temp directory, which only a server running
under your own account (LocalDB) can write to — for any other server, also set `REGIRA_SQLSERVER_BACKUP_DIRECTORY` to
a path its service account can write. The other fixtures need nothing.

Leave it out with `--filter "TestCategory!=LocalDb"`.
