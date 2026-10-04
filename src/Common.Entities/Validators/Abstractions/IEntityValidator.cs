namespace Regira.Entities.Validators.Abstractions;

/// <summary>
/// Checks an entity the write pipeline is about to save or remove, and rejects the write by adding errors to the
/// context. Validators run after every prepper — the global ones and a <c>Related()</c> sync included — and on
/// <c>Remove</c>, where no prepper runs. Every validator in scope runs, in registration order; when any of them added
/// an error, the write service throws one <c>EntityInputException&lt;TEntity&gt;</c> carrying all of them, which the
/// web layers answer with a 400.
/// <para>
/// A validator reads and never writes: <see cref="IEntityValidatorContext.Item"/> is the instance that gets saved, so a
/// value a validator sets is still written. Changing the entity is a prepper's job.
/// </para>
/// </summary>
public interface IEntityValidator
{
    /// <summary>Checks <see cref="IEntityValidatorContext.Item"/> and adds an error for every rule it breaks.</summary>
    Task Validate(IEntityValidatorContext context, CancellationToken token = default);
}

/// <inheritdoc cref="IEntityValidator"/>
/// <typeparam name="TScope">
/// The entity type to check, or a type it derives from or implements — a validator on <c>IHasTenantId</c> checks every
/// entity implementing it, whichever service saves it. The item's runtime type decides, so a validator on <c>Person</c>
/// also runs when a <c>Person</c> is saved through the <c>Party</c> service.
/// </typeparam>
/// <remarks>
/// The pipeline calls the untyped <see cref="IEntityValidator.Validate"/>, which this interface forwards to the typed
/// overload — implement the typed one only. A class implementing it for two scopes has to implement the untyped one
/// itself, since either forward would do.
/// </remarks>
public interface IEntityValidator<in TScope> : IEntityValidator
{
    /// <inheritdoc cref="IEntityValidator.Validate"/>
    Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default);

    // the pipeline builds the context over the item's runtime type, which is in this validator's scope
    Task IEntityValidator.Validate(IEntityValidatorContext context, CancellationToken token)
        => Validate((IEntityValidatorContext<TScope>)context, token);
}
