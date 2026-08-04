namespace Regira.System.Abstractions;

public interface IProcessHelper
{
    IProcessOutput ExecuteCommand(string command, bool waitForOutput = false);
    IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null);
}