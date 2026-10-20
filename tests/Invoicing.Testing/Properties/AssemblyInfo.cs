using NUnit.Framework;

// Fixtures run concurrently; the tests inside one fixture stay serial.
[assembly: Parallelizable(ParallelScope.Fixtures)]
