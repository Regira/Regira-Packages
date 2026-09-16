using NUnit.Framework;

// Each fixture instance owns its own ProviderHarness, container and ServiceProvider, so the PostgreSQL
// and SQL Server containers start concurrently instead of one after the other.
[assembly: Parallelizable(ParallelScope.Fixtures)]
