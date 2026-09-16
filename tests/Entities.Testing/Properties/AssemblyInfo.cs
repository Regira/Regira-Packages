using NUnit.Framework;

// Fixtures run concurrently; each opens its own in-memory SQLite connection, so they share no database.
// The two fixtures that write the process-wide DateTimeDefaults.UseUtc policy are marked
// [NonParallelizable] at their declaration.
[assembly: Parallelizable(ParallelScope.Fixtures)]
