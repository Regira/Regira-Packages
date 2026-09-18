# Regira System — Process Execution

Running shell commands and executables from `Regira.System`, and reading back what they wrote.

---

### IProcessHelper / ProcessHelper

Run shell commands or executables and capture their output.

```csharp
public interface IProcessHelper
{
    IProcessOutput ExecuteCommand(string command, bool waitForOutput = false);
    IProcessOutput ExecuteCommand(string command, IDictionary<string, string> environment, bool waitForOutput = false);
    IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null);
    IProcessOutput ExecuteFile(string filename, IDictionary<string, string> environment, bool waitForOutput = false, string? arguments = null);
}
```

`ProcessHelper` is the default implementation. `ExecuteCommand` writes the command to a temporary `.bat` file (in `Options.TempFolder`, or the system's temp folder), executes it and deletes it again — Windows only; every call has a file of its own, so one instance serves concurrent calls. `ExecuteFile` starts the given executable directly, on any platform. Pass `waitForOutput: true` to capture stdout/stderr — both streams are drained at the same time, so a process that writes more than a pipe buffer holds to one of them (a command-line tool logging its progress to stderr, say) cannot stall the call. The text is reassembled from line events, so it carries the platform's line ending and a trailing newline rather than the exact bytes the process wrote — trim it before comparing.

The `environment` overloads set variables on the process instead of on the command line — that is where a value belongs when it must not be written to the generated script, such as a password. They are default interface methods, so a custom `IProcessHelper` keeps compiling. One that does not override `ExecuteCommand` has the script set the variables instead (`set "KEY=VALUE"` ahead of the command, which is batch syntax — an implementation running another shell has to override it): they reach the process just the same, but their values end up wherever that implementation writes the command. One that does not override `ExecuteFile` throws — there is no command to set them from, and a variable silently dropped surfaces as a failure somewhere else entirely.

```csharp
IProcessHelper processHelper = new ProcessHelper(new ProcessHelper.Options
{
    TempFolder = @"C:\Temp"   // optional; holds the temporary .bat files, created when missing and left in place
});

IProcessOutput result = processHelper.ExecuteCommand("dotnet --version", waitForOutput: true);
Console.WriteLine(result.Output);     // captured stdout
Console.WriteLine(result.ExitCode);   // process exit code

var environment = new Dictionary<string, string> { ["PGPASSWORD"] = "pass" };
processHelper.ExecuteCommand("pg_dump --no-password mydb", environment);
```

### IProcessOutput / ProcessOutput

```csharp
public interface IProcessOutput
{
    string? Output { get; set; }
    string? Error { get; set; }
    int ExitCode { get; set; }
}
```

`Output` and `Error` are only populated when `waitForOutput` is `true`.

### ProcessHelperExtensions

Open a path or an `IBinaryFile` with the OS default application (a file without a path is written to a temp file first):

```csharp
IProcessHelper processHelper = new ProcessHelper();
IBinaryFile binaryFile = new BinaryFileItem { FileName = "invoice.pdf" };

processHelper.OpenFileByOS(@"C:\docs\invoice.pdf");
processHelper.OpenFileByOS(binaryFile);
```

---

## Overview

1. [Index](../README.md) — Overview, projects, and installation
1. **[Process Execution](processes.md)** — Running commands and executables, capturing their output
1. [Hosting](hosting.md) — `WebHostOptions`, background task queues, Windows Service installer
1. [Project Files](projects.md) — Parsing and managing `.csproj` files

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
