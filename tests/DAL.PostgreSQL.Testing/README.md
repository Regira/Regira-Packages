# DAL.PostgreSQL.Testing

NUnit tests for [DAL.PostgreSQL](../../src/DAL.PostgreSQL/README.md). `PgToolArgumentsTests` checks how the backup
and restore services start `pg_dump`/`pg_restore`; `PgDatabaseTests` runs the database operations around a restore
(lookup, create, drop, quoted names) against a real PostgreSQL server in a Testcontainers `postgres:16-alpine`
container. Both stub the tools themselves, so no PostgreSQL client binaries are needed.

## Running

```bash
dotnet test tests/DAL.PostgreSQL.Testing
```

`PgDatabaseTests` (category `Containers`) needs Docker. It runs only when `REGIRA_PROVIDER_TESTS=containers`, which
the repository's `Regira.runsettings` sets for every `dotnet test`, and it skips when the variable is unset or the
container cannot start. `REGIRA_CONTAINER_REUSE=1` keeps the container between runs — see
[CONTRIBUTING.md](../../CONTRIBUTING.md). Leave it out with `--filter "TestCategory!=Containers"`.
