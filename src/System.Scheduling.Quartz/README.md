# Regira.System.Scheduling.Quartz

Quartz.NET backend for [Regira System](https://regira.github.io/Regira-Packages/src/Common.System/) scheduling, built on [Quartz](https://www.nuget.org/packages/Quartz) 4. `UseQuartz()` makes `IJobScheduler` run its jobs (`IJobHandler`, `IJobHandler<TInput>`) on the Quartz scheduler the application registers itself, with its own job store and clustering. The jobs and the `AddScheduling` declarations are the ones the built-in backend runs.

## Installation

```xml
<PackageReference Include="Regira.System.Scheduling.Quartz" Version="6.*" />
```

Targets `net10.0` and later, as Quartz 4 does. The application calls `AddQuartz(...)` and `AddQuartzHostedService()` with its own configuration, then `AddScheduling(...).UseQuartz()`.

## Documentation

- [Scheduling](https://regira.github.io/Regira-Packages/src/Common.System/docs/scheduling.html) — job handlers, the cron subset, recurring jobs, how a job is stored in Quartz, and what each backend does with a failed or missed run

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
