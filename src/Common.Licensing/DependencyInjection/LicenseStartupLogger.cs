using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Licensing.Models;

namespace Regira.Licensing.DependencyInjection;

/// <summary>
/// Writes the <c>[Regira] License — …</c> line of each registered <see cref="License"/> through <c>ILogger</c> on
/// host start. <c>UseRegira</c> writes the same line to the console while services are registered, before a logger
/// exists, so without this a logging pipeline (a file sink, Seq) never holds it.
/// </summary>
internal sealed class LicenseStartupLogger(IEnumerable<License> licenses, ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Regira.Licensing");
        foreach (var license in licenses)
        {
            var (customerId, products, version, issuedAt, expiresAt, limits) = ServiceCollectionExtensions.Describe(license);
            logger.LogInformation(
                "[Regira] License — Customer: {Customer} | Products: {Products} | Version: {Version} | Issued: {Issued} | Expires: {Expires} | Limits: {Limits}",
                customerId, products, version, issuedAt, expiresAt, limits
            );
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
