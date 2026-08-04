namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Controls the startup validation of entity registrations (arity mismatches, unwired interceptors,
/// ignored search inputs). Configure via <c>UseEntities(o =&gt; o.ConfigureValidation(v =&gt; ...))</c>.
/// </summary>
public class EntityValidationOptions
{
    /// <summary>
    /// <c>null</c> (default) = run in the Development environment only; <c>true</c> = always run
    /// (opt-in for Production); <c>false</c> = never run.
    /// </summary>
    public bool? Enabled { get; set; }
    /// <summary>
    /// Whether error-severity issues stop the host with an aggregated exception (default) or are only logged.
    /// Warnings are always logged and never throw.
    /// </summary>
    public bool ThrowOnError { get; set; } = true;
}
