using NUnit.Framework;

// Fixtures run concurrently; each builds its own in-memory TestServer host (no fixed port is bound) and the
// helpers on TestHostFactory are pure factories.
[assembly: Parallelizable(ParallelScope.Fixtures)]
