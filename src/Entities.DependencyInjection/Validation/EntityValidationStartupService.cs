using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Runs all <see cref="IEntityRegistrationValidator"/>s once at host start, so mis-registrations
/// (arity mismatches, unwired interceptors, ignored search inputs) fail <c>dotnet run</c> instead of
/// surfacing as request-time 500s or silently wrong data. Active in Development by default; see
/// <see cref="EntityValidationOptions"/>. Registered by <c>UseEntities()</c>.
/// </summary>
internal sealed class EntityValidationStartupService(
    IServiceProvider provider,
    IServiceCollection services,
    IEnumerable<IEntityRegistrationValidator> validators,
    ILoggerFactory loggerFactory,
    EntityValidationOptions? options = null,
    EntityRegistrationLog? registrations = null,
    IHostEnvironment? environment = null) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = options?.Enabled ?? environment?.IsDevelopment() ?? false;
        if (!enabled)
        {
            return Task.CompletedTask;
        }

        var logger = loggerFactory.CreateLogger("Regira.Entities.Validation");
        // Inside a scope, not from the root provider: the checks that matter most inspect row-security
        // filters, and a row-security filter depends on the caller's identity — a scoped service the root
        // provider refuses to resolve. Validating from the root left exactly those filters unchecked.
        using var scope = provider.CreateScope();
        var context = new EntityValidationContext(scope.ServiceProvider, services, registrations ?? new EntityRegistrationLog());
        var issues = validators.SelectMany(v => Run(v, context, logger)).ToArray();

        foreach (var issue in issues)
        {
            // Pass the message as a structured argument, not as the template — a validation message can
            // contain a code snippet with '{'/'}' which the logging formatter would treat as a malformed
            // placeholder and throw FormatException, turning a diagnostic into a startup crash.
            switch (issue.Severity)
            {
                case EntityValidationSeverity.Info:
                    logger.LogInformation("{Message}", issue.Message);
                    break;
                case EntityValidationSeverity.Warning:
                    logger.LogWarning("{Message}", issue.Message);
                    break;
                default:
                    logger.LogError("{Message}", issue.Message);
                    break;
            }
        }

        var errors = issues.Where(i => i.Severity == EntityValidationSeverity.Error).ToArray();
        if (errors.Length > 0 && (options?.ThrowOnError ?? true))
        {
            throw new InvalidOperationException(
                $"Entity registration validation found {errors.Length} error(s):" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select((e, i) => $"  {i + 1}. {e.Message}")) + Environment.NewLine +
                "Disable or soften this check via UseEntities(o => o.ConfigureValidation(v => ...)).");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs one validator, downgrading a throw to a warning. A diagnostic that inspects the container has to
    /// tolerate whatever it finds there — a custom implementation, a factory that throws — and no such
    /// surprise is worth taking the host down for. Deliberate failures still travel as
    /// <see cref="EntityValidationSeverity.Error"/> issues and throw below.
    /// </summary>
    private static IEnumerable<EntityValidationIssue> Run(IEntityRegistrationValidator validator, EntityValidationContext context, ILogger logger)
    {
        try
        {
            return validator.Validate(context).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Validator} could not complete and was skipped.", validator.GetType().Name);
            return [];
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
