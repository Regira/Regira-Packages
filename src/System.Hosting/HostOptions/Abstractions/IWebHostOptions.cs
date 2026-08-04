namespace Regira.System.Hosting.HostOptions.Abstractions;

public interface IWebHostOptions : IHostOptions
{
    int? LocalPort { get; set; }
    bool SelfHosting { get; set; }
    string? RoutePrefix { get; set; }

    bool EnableSwagger { get; set; }
    bool EnableCors { get; set; }
    bool EnableHttps { get; set; }
}