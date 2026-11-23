# Regira.System.Scheduling.Hangfire

Hangfire backend for [Regira System](https://regira.github.io/Regira-Packages/src/Common.System/) scheduling, built on [Hangfire.Core](https://www.nuget.org/packages/Hangfire.Core). `UseHangfire()` makes `IJobScheduler` run its jobs (`IJobHandler`, `IJobHandler<TInput>`) on the Hangfire installation the application registers itself, with its own storage, servers, retry filter and dashboard. The jobs and the `AddScheduling` declarations are the ones the built-in backend runs.

## Installation

```xml
<PackageReference Include="Regira.System.Scheduling.Hangfire" Version="6.*" />
```

The package references `Hangfire.Core` only: the application adds `Hangfire.AspNetCore` or `Hangfire.NetCore` and a storage package, calls `AddHangfire(...)` and `AddHangfireServer()`, then `AddScheduling(...).UseHangfire()`.

Hangfire.Core is licensed under LGPL-3.0, or commercially by its authors. It is this package's dependency alone; the scheduling contract and the built-in backend carry none of it.

## Documentation

- [Scheduling](https://regira.github.io/Regira-Packages/src/Common.System/docs/scheduling.html) — job handlers, the cron subset, recurring jobs, Hangfire's retries and lock, and what each backend does with a failed or missed run

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
