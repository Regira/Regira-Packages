using NUnit.Framework;

// Fixtures run concurrently; they assert over service collections they each build themselves.
// LicenseEnforcementTests writes a process-wide test key and is marked [NonParallelizable] at its declaration.
[assembly: Parallelizable(ParallelScope.Fixtures)]
