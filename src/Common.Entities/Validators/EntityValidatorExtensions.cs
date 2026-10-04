using Regira.Entities.Models;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.Validators;

public static class EntityValidatorExtensions
{
    /// <summary>
    /// Runs every validator in scope of <paramref name="item"/>, in the order given, against one context, and throws when
    /// any of them added an error. The scope is decided by the item's runtime type: a validator applies when it is scoped
    /// to that type, a type it derives from or an interface it implements — and, as an <see cref="ISelectiveEntityValidator"/>,
    /// covers it.
    /// <para>
    /// <c>EntityWriteService</c> calls this for every <c>Add</c>, <c>Modify</c> and <c>Remove</c>. A service writing
    /// to another store — a custom <c>IEntityRepository</c> — imports <c>IEnumerable&lt;IEntityValidator&gt;</c> and
    /// makes the same call before it writes.
    /// </para>
    /// </summary>
    /// <param name="validators">The registered validators, e.g. an imported <c>IEnumerable&lt;IEntityValidator&gt;</c>.</param>
    /// <param name="item">The entity about to be written.</param>
    /// <param name="original">The row as stored, for <see cref="EntityWriteOperation.Modify"/>.</param>
    /// <param name="operation">The write being checked.</param>
    /// <param name="token">Cancels the validators.</param>
    /// <exception cref="EntityInputException{TEntity}">
    /// Closed over <typeparamref name="TEntity"/>, whatever scope the failing validators were declared on, with every
    /// error in <see cref="EntityInputException.Errors"/>.
    /// </exception>
    public static async Task ValidateItem<TEntity>(this IEnumerable<IEntityValidator> validators, TEntity item, TEntity? original,
        EntityWriteOperation operation, CancellationToken token = default)
        where TEntity : class
    {
        var itemType = item.GetType();
        IEntityValidatorContext? context = null;
        foreach (var validator in validators)
        {
            if (EntityValidatorScope.RunsFor(validator, itemType))
            {
                context ??= EntityValidatorContextFactory.Create(item, original, operation);
                await validator.Validate(context, token);
            }
        }

        if (context is { Errors.Count: > 0 })
        {
            throw ToException(item, context.Errors);
        }
    }

    /// <summary>
    /// Whether any of <paramref name="validators"/> runs for an item of <paramref name="itemType"/> — the rule
    /// <see cref="ValidateItem{TEntity}"/> applies. When none does, the write cannot be refused, so a service can skip work
    /// it does only to support a refusal: <c>EntityWriteService</c> keeps no undo log.
    /// </summary>
    public static bool AnyApplyTo(this IEnumerable<IEntityValidator> validators, Type itemType)
        => validators.Any(validator => EntityValidatorScope.RunsFor(validator, itemType));

    private static EntityInputException<TEntity> ToException<TEntity>(TEntity item, IReadOnlyList<EntityInputError> errors)
    {
        var summary = string.Join("; ", errors.Select(e => e.Key.Length == 0 ? e.Message : $"{e.Key}: {e.Message}"));
        var exception = new EntityInputException<TEntity>($"{typeof(TEntity).Name} failed validation: {summary}")
        {
            Item = item
        };
        foreach (var error in errors)
        {
            exception.Errors.Add(error);
        }
        return exception;
    }
}
