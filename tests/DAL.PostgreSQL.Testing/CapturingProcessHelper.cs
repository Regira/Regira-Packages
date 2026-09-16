using System.Text.RegularExpressions;
using NUnit.Framework;
using Regira.System;
using Regira.System.Abstractions;

namespace DAL.PostgreSQL.Testing;

/// <summary>
/// Stands in for <c>pg_dump</c> and <c>pg_restore</c>: reports success without starting a process, and captures the
/// executable, its arguments and the bytes of the backup file the arguments point at.
/// </summary>
/// <remarks>
/// Reading the file here is the point of the stub: the real tool opens the backup itself, so a restore
/// that still holds the file open (or has not flushed it) fails at exactly this moment.
/// </remarks>
public class CapturingProcessHelper : IProcessHelper
{
    public string? FileName { get; private set; }
    public string? Arguments { get; private set; }
    public string? SourcePath { get; private set; }
    public byte[]? SourceBytes { get; private set; }
    public IDictionary<string, string>? EnvironmentVariables { get; private set; }
    /// <summary>
    /// Exit code to report for the `pg_restore --list` call that reads the archive before the target database is
    /// touched. Non-zero stands in for a corrupt or truncated backup.
    /// </summary>
    public int ListExitCode { get; set; }

    /// <summary>
    /// The services start the tools directly; a shell command would run through a generated <c>.bat</c>, which no
    /// platform but Windows can start.
    /// </summary>
    public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
        => throw new AssertionException($"The tools must be started directly, not through a shell command: {command}");

    public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
    {
        FileName = filename;
        Arguments = arguments;
        var isList = arguments?.StartsWith("--list") ?? false;
        // last quoted argument: the archive, for both pg_restore calls
        SourcePath = Regex.Matches(arguments ?? string.Empty, "\"([^\"]*)\"").LastOrDefault()?.Groups[1].Value;
        if (SourcePath != null && File.Exists(SourcePath) && !filename.Contains("pg_dump"))
        {
            SourceBytes = File.ReadAllBytes(SourcePath);
        }

        return new ProcessOutput { ExitCode = isList ? ListExitCode : 0 };
    }

    /// <summary>
    /// Overridden so the stub records the variables, the way <see cref="ProcessHelper"/> hands them to the process.
    /// Inheriting the default would throw for any variable given.
    /// </summary>
    public IProcessOutput ExecuteFile(string filename, IDictionary<string, string> environment, bool waitForOutput = false, string? arguments = null)
    {
        EnvironmentVariables = environment;
        return ExecuteFile(filename, waitForOutput, arguments);
    }
}
