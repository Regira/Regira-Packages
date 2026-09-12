using System.Text.RegularExpressions;
using Regira.System;
using Regira.System.Abstractions;

namespace DAL.PostgreSQL.Testing;

/// <summary>
/// Stands in for <c>pg_restore.exe</c>: reports success without starting a process, and captures the
/// command and the bytes of the backup file the command points at.
/// </summary>
/// <remarks>
/// Reading the file here is the point of the stub: the real tool opens the backup itself, so a restore
/// that still holds the file open (or has not flushed it) fails at exactly this moment.
/// </remarks>
public class CapturingProcessHelper : IProcessHelper
{
    public string? Command { get; private set; }
    public string? SourcePath { get; private set; }
    public byte[]? SourceBytes { get; private set; }
    public IDictionary<string, string>? EnvironmentVariables { get; private set; }
    /// <summary>
    /// Exit code to report for the `pg_restore --list` call that reads the archive before the target database is
    /// touched. Non-zero stands in for a corrupt or truncated backup.
    /// </summary>
    public int ListExitCode { get; set; }

    public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
    {
        Command = command;
        // last quoted argument of the pg_restore command line
        SourcePath = Regex.Matches(command, "\"([^\"]*)\"").LastOrDefault()?.Groups[1].Value;
        if (SourcePath != null)
        {
            SourceBytes = File.ReadAllBytes(SourcePath);
        }

        return new ProcessOutput { ExitCode = command.Contains("--list") ? ListExitCode : 0 };
    }

    /// <summary>
    /// Overridden so the stub keeps the variables out of the command, the way <see cref="ProcessHelper"/> does.
    /// Inheriting the default instead would have the command carry `set "PGPASSWORD=..."` and quietly test the
    /// fallback rather than the path the services actually run on.
    /// </summary>
    public IProcessOutput ExecuteCommand(string command, IDictionary<string, string> environment, bool waitForOutput = false)
    {
        EnvironmentVariables = environment;
        return ExecuteCommand(command, waitForOutput);
    }

    public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
        => new ProcessOutput { ExitCode = 0 };
}
