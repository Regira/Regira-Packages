using Regira.System.Hosting.HostOptions.Abstractions;

namespace Regira.System.Hosting.HostOptions.Models;

public class WebHostOptions : HostOptions, IWebHostOptions
{
    public int? LocalPort { get; set; }
    public bool SelfHosting { get; set; }
    public string? RoutePrefix { get; set; }

    public bool EnableSwagger { get; set; } = true;
    public bool EnableCors { get; set; }
    public bool EnableHttps { get; set; }
}