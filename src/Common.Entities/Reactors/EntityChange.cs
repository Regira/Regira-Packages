using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.Reactors;

/// <summary>
/// A committed change of a <typeparamref name="TEntity"/> row. The save pipeline builds these for its reactors;
/// construct one yourself to unit-test a reactor.
/// </summary>
/// <param name="kind">What the save did to the row.</param>
/// <param name="entity">The row as committed (for a delete, as it was stored).</param>
/// <param name="original">The row as stored before the save; <c>null</c> for an insert.</param>
/// <param name="changedProperties">For an update, the properties whose value changed.</param>
public class EntityChange<TEntity>(EntityChangeKind kind, TEntity entity, TEntity? original = null, IReadOnlyCollection<string>? changedProperties = null)
    : IEntityChange<TEntity>
    where TEntity : class
{
    public EntityChangeKind Kind => kind;
    public TEntity Entity => entity;
    public TEntity? Original => original;
    public IReadOnlyCollection<string> ChangedProperties { get; } = changedProperties ?? [];

    object IEntityChange.Entity => Entity;
    object? IEntityChange.Original => Original;
}
