# Hosting.Testing

NUnit tests for [System.Hosting](../../src/System.Hosting/README.md): the typed service provider
(`AddTypedProvider` / `GetTypedImplementation`), which resolves an implementation keyed by a type, the background
queue, and scheduling.

The scheduling tests are in `Scheduling/`:

- `CronScheduleTests` — the portable cron subset: what parses, what is refused, and the message.
- `InMemoryJobSchedulerTests` — the built-in backend on a fake clock (`FakeTimeProvider`), so nothing waits for time.
- `JobSchedulerContractTests` — the portable contract as an abstract fixture against an `IJobScheduler`, with one
  subclass per backend: the built-in one, Hangfire on its in-memory storage and Quartz on its in-memory job store.
- `QuartzCronTests` — the translation to the Quartz dialect, with Cronos as the oracle for the occurrences.

## Running

```bash
dotnet test tests/Hosting.Testing
```

No external requirements.
