namespace Regira.Entities.Validators.Abstractions;

/// <summary>
/// Base class for an <see cref="IEntityValidator{TScope}"/>: override <see cref="Validate"/>, and <see cref="CanValidate"/>
/// to check some items only (by default every item in scope). It has one scope: a validator for two implements
/// <see cref="IEntityValidator{TScope}"/> for each and the untyped <see cref="IEntityValidator.Validate"/> directly —
/// <c>AddValidator</c> refuses a subclass that adds a second scope, which this class would never run.
/// </summary>
public abstract class EntityValidatorBase<TScope> : IEntityValidator<TScope>
    where TScope : class
{
    /// <summary>Whether to check <paramref name="item"/> — evaluated for every item in scope, before <see cref="Validate"/>.</summary>
    public virtual bool CanValidate(TScope item) => true;
    public abstract Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default);

    Task IEntityValidator.Validate(IEntityValidatorContext context, CancellationToken token)
    {
        var typed = (IEntityValidatorContext<TScope>)context;
        return CanValidate(typed.Item) ? Validate(typed, token) : Task.CompletedTask;
    }
}
