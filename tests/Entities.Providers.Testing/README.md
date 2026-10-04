# Entities.Providers.Testing

NUnit integration tests that run the [Regira Entities](../../src/Common.Entities/README.md) query pipeline, wired by
[Entities.DependencyInjection](../../src/Entities.DependencyInjection/README.md), against SQLite, PostgreSQL and SQL
Server. `ProviderQueryPipelineTests` covers where providers diverge — global filters, capability-interface sorting,
`q` LIKE translation, multi-search-object unions, paging; `ProviderReactorTests` covers reactors on each provider's
own transactions.

## Running

```bash
dotnet test tests/Entities.Providers.Testing
```

Each fixture runs once per provider. SQLite runs in memory. PostgreSQL (`postgres:16-alpine`) and SQL Server
(`mcr.microsoft.com/mssql/server:2022-latest`) run in Testcontainers containers and need Docker; they run only when
`REGIRA_PROVIDER_TESTS=containers`, which the repository's `Regira.runsettings` sets, and skip when the variable is
unset or a container cannot start. `REGIRA_CONTAINER_REUSE=1` keeps the containers between runs — see
[CONTRIBUTING.md](../../CONTRIBUTING.md).

Both fixtures carry the `Containers` category as a whole, so `--filter "TestCategory!=Containers"` leaves out the
SQLite runs as well.
