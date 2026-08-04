namespace Regira.System.Abstractions;

public interface IProcessOutput
{
    string? Output { get; set; }
    string? Error { get; set; }
    int ExitCode { get; set; }
}