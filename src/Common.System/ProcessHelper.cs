using System.Diagnostics;
using Regira.System.Abstractions;

namespace Regira.System;

public class ProcessHelper : IProcessHelper
{
    public class Options
    {
        /// <summary>
        /// Folder to store temporary .bat-file which is removed after execution
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
        _tempFolder = options?.TempFolder ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _onOutputDataReceived = options?.OnOutputDataReceived;
    }

    public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
    {
        var batFilePath = Path.Combine(_tempFolder, $"{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}.bat");
        var directory = Path.GetDirectoryName(batFilePath) ?? throw new Exception("Invalid tempFolder for temporary batFile");
        var deleteDir = false;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            deleteDir = true;
        }
        File.WriteAllText(batFilePath, command);
        var output = ExecuteFile(batFilePath, waitForOutput);
        if (deleteDir)
        {
            Directory.Delete(directory, true);
        }
        else
        {
            File.Delete(batFilePath);
        }

        return output;
    }
    public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = waitForOutput,
                RedirectStandardError = waitForOutput,
                Arguments = arguments ?? string.Empty
            }
        };
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += process_OutputDataReceived;
        process.Start();
        try
        {
            string? output = null, error = null;
            if (waitForOutput)
            {
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
            }
            process.WaitForExit();
            return new ProcessOutput
            {
                Output = output,
                Error = error,
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