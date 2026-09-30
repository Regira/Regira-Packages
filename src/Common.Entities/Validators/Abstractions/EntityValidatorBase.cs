namespace Regira.Entities.Validators.Abstractions;

/// <summary>
/// Base class for an <see cref="IEntityValidator{TScope}"/>: override <see cref="Validate"/>, and <see cref="CanValidate"/>
/// to check some items only (by default every item in scope).
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
