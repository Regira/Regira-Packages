namespace Regira.Entities.Reactors.Abstractions;

/// <summary>What a committed save did to a row.</summary>
public enum EntityChangeKind
{
    /// <summary>The row was inserted.</summary>
    Added,
    /// <summary>
    /// The row was updated. A soft delete of an <c>IArchivable</c> entity is an update too — test it with
    /// <c>change.ChangedTo(x =&gt; x.IsArchived, true)</c>.
    /// </summary>
    Modified,
    /// <summary>The row was removed from the database.</summary>
    Deleted
}

/// <summary>
/// One row a committed save changed, as an <see cref="IEntityReactor"/> receives it. Values are detached
/// snapshots of the entity's scalar and complex properties — navigations are not loaded, and changing a
/// snapshot persists nothing.
/// </summary>
public interface IEntityChange
{
    /// <summary>What the save did to the row.</summary>
    EntityChangeKind Kind { get; }
    /// <summary>
    /// The row as committed, with store-generated keys and values filled in. For <see cref="EntityChangeKind.Deleted"/>,
    /// the row as it was stored when it was removed.
    /// </summary>
    object Entity { get; }
    /// <summary>
    /// The row as it was stored before the save — <c>null</c> for <see cref="EntityChangeKind.Added"/>.
    /// </summary>
    object? Original { get; }
    /// <summary>
    /// For <see cref="EntityChangeKind.Modified"/>, the properties whose committed value differs from the stored one,
    /// a complex type's members as a dotted path (<c>Address.City</c>). Empty for <see cref="EntityChangeKind.Added"/>
    /// and <see cref="EntityChangeKind.Deleted"/>, which change the whole row — test <see cref="Kind"/> for those.
    /// </summary>
    IReadOnlyCollection<string> ChangedProperties { get; }
}

/// <inheritdoc cref="IEntityChange"/>
/// <typeparam name="TEntity">
/// The entity type, or any type it derives from or implements — a change of an <c>Order</c> is also an
/// <c>IEntityChange&lt;IHasTimestamps&gt;</c>.
/// </typeparam>
public interface IEntityChange<out TEntity> : IEntityChange
{
    /// <inheritdoc cref="IEntityChange.Entity"/>
    new TEntity Entity { get; }
    /// <inheritdoc cref="IEntityChange.Original"/>
    new TEntity? Original { get; }
}
