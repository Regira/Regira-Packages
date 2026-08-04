using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Regira.Entities.DependencyInjection.Licensing;

/// <summary>
/// Emits a single startup log line reporting the entity-registration tally and resolved license tier,
/// so the free/paid budget is visible before any limit is hit. Runs on host start (works for web,
/// console and worker hosts); the counts are final by then because all <c>For&lt;&gt;()</c> registrations
/// run during DI configuration.
/// <para>
/// The line names the entities, not just the counts. It is the only confirmation that a hand-written
/// registration budget matches what was actually registered, so when the two disagree the reader has to be
/// able to see <em>which</em> side is wrong — a bare pair of numbers leaves "did my owned children consume
/// a slot?" unanswerable, and on a machine running several apps it does not even prove the line is yours.
/// </para>
/// </summary>
internal sealed class EntityLicenseStartupLogger(EntityLicenseValidator validator, ILoggerFactory loggerFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Regira.Entities");
        // One line, no embedded newlines: a structured sink renders the template verbatim, so a multi-line
        // template reads as escaped `\n` in Seq/App Insights while gaining nothing a console reader needs.
        logger.LogInformation(
            "Regira.Entities: {Simple} simple / {Complex} complex registered → tier = {Tier} (simple: {SimpleNames}; complex: {ComplexNames})",
            validator.SimpleCount, validator.ComplexCount, validator.Tier, validator.SimpleNames, validator.ComplexNames
        );
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
