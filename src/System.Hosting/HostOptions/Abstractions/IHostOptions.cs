namespace Regira.System.Hosting.HostOptions.Abstractions;

public interface IHostOptions
{
    string? ServiceName { get; set; }
    string Mode { get; set; }
}