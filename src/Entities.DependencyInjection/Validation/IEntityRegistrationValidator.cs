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
    private (IReadOnlyList<ValidatedDbContext> Resolved, IReadOnlyList<string> Unresolved)? _dbContexts;

    public IServiceProvider Provider => provider;
    public IServiceCollection Services => services;
    public EntityRegistrationLog Registrations => registrations;

    /// <summary>
    /// The registered contexts the model checks inspect, resolved once per run from <see cref="Provider"/>. A context
    /// that cannot be resolved is left out, and the run reports it once (<see cref="UnresolvedDbContexts"/>).
    /// </summary>
    internal IReadOnlyList<ValidatedDbContext> DbContexts => (_dbContexts ??= ValidationContextTypes.Resolve(this)).Resolved;

    /// <summary>Why a context <see cref="DbContexts"/> left out could not be resolved; empty until a check asks for them.</summary>
    internal IReadOnlyList<string> UnresolvedDbContexts => _dbContexts?.Unresolved ?? [];
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
