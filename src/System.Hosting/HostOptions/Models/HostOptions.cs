using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Regira.System.Hosting.HostOptions.Abstractions;
using Regira.System.Hosting.HostOptions.Constants;

namespace Regira.System.Hosting.HostOptions.Models;

public class HostOptions : IHostOptions
{
    /// <summary>
    /// The identifier for this app, also used as display name when using as a Windows Service
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Similar to <see cref="HostingEnvironment.EnvironmentName" />, but configurable in <see cref="IConfiguration"/>
    /// </summary>
    public string Mode { get; set; } = HostingModes.Production;
}