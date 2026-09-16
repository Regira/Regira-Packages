namespace Regira.System.Abstractions;

public interface IProcessHelper
{
    IProcessOutput ExecuteCommand(string command, bool waitForOutput = false);
    /// <summary>
    /// Runs a command with extra environment variables set on the process.
    /// </summary>
    /// <param name="command">Command to run</param>
    /// <param name="environment">Variables to add to the process environment</param>
    /// <param name="waitForOutput">Capture stdout/stderr</param>
    /// <remarks>
    /// Use this for values that must not appear in the command itself — a password, say, which
    /// <see cref="ExecuteCommand(string, bool)"/> would write to the temporary script it generates.
    /// An implementation that does not override this sets them from the script instead, which reaches the process
    /// just as well but writes the values wherever the implementation writes the command. Those lines are Windows
    /// batch syntax (<c>set "KEY=VALUE"</c>), so the fallback assumes an implementation that runs the command as a
    /// <c>.bat</c> file, as <c>ProcessHelper</c> does; any other shell has to override this.
    /// </remarks>
    IProcessOutput ExecuteCommand(string command, IDictionary<string, string> environment, bool waitForOutput = false)
    {
        // Nothing here can reach the process, so the script sets the variables itself — the same lines the caller
        // would have written by hand, and the same exposure. An implementation that can reach the process
        // (ProcessHelper) overrides this and keeps the values out of the command altogether.
        var assignments = environment.Select(variable => $"set \"{variable.Key}={ForScript(variable.Value, variable.Key)}\"");
        return ExecuteCommand(string.Join(Environment.NewLine, assignments.Append(command)), waitForOutput);
    }
    IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null);
    /// <summary>
    /// Runs an executable with extra environment variables set on the process.
    /// </summary>
    /// <param name="filename">Executable to start</param>
    /// <param name="environment">Variables to add to the process environment</param>
    /// <param name="waitForOutput">Capture stdout/stderr</param>
    /// <param name="arguments">Arguments to pass to the executable</param>
    /// <remarks>
    /// There is no command to set them from here, so an implementation that does not override this throws rather than
    /// start the executable without them — a variable silently dropped is a failure somewhere else entirely.
    /// </remarks>
    /// <exception cref="NotSupportedException">The implementation does not override this and variables were given</exception>
    IProcessOutput ExecuteFile(string filename, IDictionary<string, string> environment, bool waitForOutput = false, string? arguments = null)
        => environment.Any()
            ? throw new NotSupportedException($"{GetType().Name} starts an executable without an environment of its own. Override {nameof(ExecuteFile)}(string, IDictionary<string, string>, bool, string) to pass {string.Join(", ", environment.Keys)} on to it.")
            : ExecuteFile(filename, waitForOutput, arguments);

    /// <summary>
    /// A value as a script can carry it: <c>set "KEY=VALUE"</c> survives spaces and the shell's own operators, a
    /// doubled percent sign is read back as one, and a quote or a line break cannot be expressed at all.
    /// </summary>
    private static string ForScript(string value, string name)
    {
        if (value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
        {
            throw new ArgumentException($"The value of {name} holds a quote or a line break, which a script cannot set. Use an IProcessHelper that sets environment variables on the process itself.", "environment");
        }

        return value.Replace("%", "%%");
    }
}
