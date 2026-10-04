# Regira.System.Hosting

Hosting extensions for [Regira System](https://regira.github.io/Regira-Packages/src/Common.System/), built on [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting). `UseWebHostOptions()` binds the `Hosting` configuration section to `WebHostOptions`; `UseBackgroundQueue()` registers a queue for background work — `IBackgroundTaskQueue`, or `IBackgroundQueueManager<TTask>` for typed tasks with progress and a status to poll; `AddWindowsServiceInstaller()` writes install and uninstall scripts that register the app as a Windows Service. They apply to any host, including an ASP.NET Core application.

## Installation

```xml
<PackageReference Include="Regira.System.Hosting" Version="6.*" />
```

## Documentation

- [Hosting](https://regira.github.io/Regira-Packages/src/Common.System/docs/hosting.html) — the `WebHostOptions` settings, background task queues and the Windows Service installer
- [Background export queue](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html#example-5-background-export-queue) — enqueuing a typed task from a controller and polling its status

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
