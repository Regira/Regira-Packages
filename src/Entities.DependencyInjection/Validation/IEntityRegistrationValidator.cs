using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.DependencyInjection.Validation;

public enum EntityValidationSeverity
{
    /// <summary>Logged at startup as information; a deliberate-looking configuration worth confirming.</summary>
    Info,
    /// <summary>Logged at startup; never stops the host.</summary>
    Warning,
    /// <summary>Logged at startup and thrown (aggregated) when <see cref="EntityValidationOptions.ThrowOnError"/> is set.</summary>
    Error
}

public sealed record EntityValidationIssue(EntityValidationSeverity Severity, string Message);

public sealed class EntityValidationContext(IServiceProvider provider, IServiceCollection services, EntityRegistrationLog registrations)
{
    public IServiceProvider Provider => provider;
    public IServiceCollection Services => services;
    public EntityRegistrationLog Registrations => registrations;
}

/// <summary>
/// A startup check over the entity registrations, run once by the validation hosted service
/// (Development-only unless configured otherwise — see <see cref="EntityValidationOptions"/>).
/// Register implementations via <c>TryAddEnumerable(ServiceDescriptor.Singleton&lt;IEntityRegistrationValidator, ...&gt;())</c>.
/// </summary>
public interface IEntityRegistrationValidator
{
    IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context);
}
