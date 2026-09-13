using NUnit.Framework;

// Fixtures run concurrently; the tests inside one fixture stay serial. That distinction is load-bearing
// here: several fixtures drive a remote store (GitHub, Azure) whose API is rate limited, and keeping a
// fixture's own tests serial holds the request rate per service exactly where it was. Do not widen any
// remote-store fixture to ParallelScope.All.
[assembly: Parallelizable(ParallelScope.Fixtures)]
