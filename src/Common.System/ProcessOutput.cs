using Regira.System.Abstractions;

namespace Regira.System;

public struct ProcessOutput : IProcessOutput
{
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int ExitCode { get; set; }
}