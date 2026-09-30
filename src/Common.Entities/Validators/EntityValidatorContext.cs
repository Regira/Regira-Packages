using System.Collections.Concurrent;
using System.Reflection;
using Regira.Entities.Models;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.Validators;

/// <summary>
/// The context of one write of a <typeparamref name="TEntity"/>. The write pipeline builds these for its validators,
/// closed over the item's runtime type; construct one yourself to unit-test a validator.
/// </summary>
/// <param name="operation">The write being checked.</param>
/// <param name="item">The entity about to be written.</param>
/// <param name="original">The row as stored, for <see cref="EntityWriteOperation.Modify"/>.</param>
public class EntityValidatorContext<TEntity>(EntityWriteOperation operation, TEntity item, TEntity? original = null)
    : IEntityValidatorContext<TEntity>
    where TEntity : class
{
    private readonly List<EntityInputError> _errors = [];

    public TEntity Item => item;
    public TEntity? Original => original;
    public EntityWriteOperation Operation => operation;
    public IReadOnlyList<EntityInputError> Errors => _errors;

    public void AddError(string key, string message) => _errors.Add(new EntityInputError(key ?? string.Empty, message));

    object IEntityValidatorContext.Item => Item;
    object? IEntityValidatorContext.Original => Original;
}

/// <summary>
/// Builds the <see cref="EntityValidatorContext{TEntity}"/> of an item's runtime type, whatever type the caller declared
/// it as: only a context closed over the runtime type can be seen, through covariance, by a validator scoped to that type
/// (<c>Person</c>) as well as by one scoped to anything it derives from or implements (<c>Party</c>, <c>IHasTenantId</c>).
/// </summary>
internal static class EntityValidatorContextFactory
{
    private delegate IEntityValidatorContext Factory(object item, object? original, EntityWriteOperation operation);

    private static readonly MethodInfo CreateTypedMethod =
        typeof(EntityValidatorContextFactory).GetMethod(nameof(CreateTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConcurrentDictionary<Type, Factory> Factories = new();

    public static IEntityValidatorContext Create(object item, object? original, EntityWriteOperation operation)
        => Factories.GetOrAdd(item.GetType(), static type => CreateTypedMethod.MakeGenericMethod(type).CreateDelegate<Factory>())
            (item, original, operation);

    private static IEntityValidatorContext CreateTyped<TEntity>(object item, object? original, EntityWriteOperation operation)
        where TEntity : class
        => new EntityValidatorContext<TEntity>(operation, (TEntity)item, original as TEntity);
}
