using System.Diagnostics;
using System.Text;
using Regira.System.Abstractions;

namespace Regira.System;

public class ProcessHelper : IProcessHelper
{
    public class Options
    {
        /// <summary>
        /// Folder for the temporary .bat file <see cref="ExecuteCommand(string, bool)"/> writes and removes again.
        /// Created when missing, and left in place. Defaults to the system's temp folder.
        /// </summary>
        public string? TempFolder { get; set; }
        public Action<object, DataReceivedEventArgs>? OnOutputDataReceived { get; set; }
    }

    private readonly string _tempFolder;
    private readonly Action<object, DataReceivedEventArgs>? _onOutputDataReceived;
    /// <summary>
    /// ProcessHelper
    /// </summary>
    /// <param name="options"></param>
    public ProcessHelper(Options? options = null)
    {
        _tempFolder = options?.TempFolder ?? Path.GetTempPath();
        _onOutputDataReceived = options?.OnOutputDataReceived;
    }

    /// <summary>
    /// Runs <paramref name="command"/> as a Windows batch file, written to <see cref="Options.TempFolder"/> and removed
    /// again. Every call has a file of its own and removes only that file, so concurrent calls on one instance never
    /// touch each other's script.
    /// </summary>
    public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
        => ExecuteCommand(command, new Dictionary<string, string>(), waitForOutput);
    /// <inheritdoc cref="ExecuteCommand(string, bool)"/>
    public IProcessOutput ExecuteCommand(string command, IDictionary<string, string> environment, bool waitForOutput = false)
    {
        Directory.CreateDirectory(_tempFolder);
        var batFilePath = Path.Combine(_tempFolder, $"regira-{Guid.NewGuid():N}.bat");
        // @echo off, or cmd repeats every line of the script back on stdout before the command's own output —
        // noise when the output is captured, and a disclosure when a line carries a secret (`set PGPASSWORD=...`)
        try
        {
            File.WriteAllText(batFilePath, $"@echo off{Environment.NewLine}{command}");
            return ExecuteFile(batFilePath, environment, waitForOutput);
        }
        finally
        {
            // the script goes whether or not the process ran
            try
            {
                File.Delete(batFilePath);
            }
            catch (Exception)
            {
                // best effort — a failure to clean up must not replace what the command reported
            }
        }
    }
    public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
        => ExecuteFile(filename, new Dictionary<string, string>(), waitForOutput, arguments);
    public IProcessOutput ExecuteFile(string filename, IDictionary<string, string> environment, bool waitForOutput = false, string? arguments = null)
    {
        // redirect when the caller wants the text back, and when a callback is waiting to be fed it
        var redirect = waitForOutput || _onOutputDataReceived != null;
        var startInfo = new ProcessStartInfo
        {
            FileName = filename,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            Arguments = arguments ?? string.Empty
        };
        // set on the process itself, so a secret never reaches the generated script
        foreach (var variable in environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        var process = new Process { StartInfo = startInfo };
        process.EnableRaisingEvents = true;

        // Line events rather than ReadToEnd: they are the only shape that can feed the caller's callback, and they
        // empty both pipes while the process is still running. Reading one stream to the end first leaves the other
        // unattended, and a process writing more than its buffer holds — a tool logging its progress to stderr, say —
        // then blocks on that write while we block on the stream it has finished with.
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        if (redirect)
        {
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }

                process_OutputDataReceived(sender, e);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };
        }

        process.Start();
        try
        {
            if (redirect)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            // the parameterless overload also waits for the readers above to drain, so the text below is complete
            process.WaitForExit();

            return new ProcessOutput
            {
                Output = waitForOutput ? outputBuilder.ToString() : null,
                Error = waitForOutput ? errorBuilder.ToString() : null,
                ExitCode = process.ExitCode
            };
        }
        finally
        {
            process.Close();
        }
    }

    protected virtual void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        _onOutputDataReceived?.Invoke(sender, e);
    }
}