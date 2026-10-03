using Regira.Entities.Models;

namespace Regira.Entities.Validators.Abstractions;

/// <summary>The write an <see cref="IEntityValidator"/> checks.</summary>
public enum EntityWriteOperation
{
    /// <summary>A new row is about to be inserted.</summary>
    Add,
    /// <summary>A stored row is about to be updated.</summary>
    Modify,
    /// <summary>A stored row is about to be removed — a soft delete of an <c>IArchivable</c> entity included.</summary>
    Remove
}

/// <summary>
/// One write, as the validators in scope receive it. All of them share one context, so the errors an earlier validator
/// added are visible to the later ones — to skip an expensive check when a cheaper one already failed.
/// </summary>
public interface IEntityValidatorContext
{
    /// <summary>
    /// The entity about to be written: after every prepper for <see cref="EntityWriteOperation.Add"/> and
    /// <see cref="EntityWriteOperation.Modify"/>; for <see cref="EntityWriteOperation.Remove"/>, the row as stored — the
    /// caller's instance when no stored row is found — so a delete by key alone is judged by the row's state.
    /// </summary>
    object Item { get; }
    /// <summary>
    /// The row as stored for <see cref="EntityWriteOperation.Modify"/>; <c>null</c> for <see cref="EntityWriteOperation.Add"/>
    /// and <see cref="EntityWriteOperation.Remove"/>.
    /// </summary>
    object? Original { get; }
    /// <summary>The write being checked.</summary>
    EntityWriteOperation Operation { get; }
    /// <summary>The errors added so far, by every validator of this write.</summary>
    IReadOnlyList<EntityInputError> Errors { get; }
    /// <summary>
    /// Rejects the write. <paramref name="key"/> is the property path (<c>CustomerId</c>, <c>Lines[0].Quantity</c>), or
    /// <see cref="string.Empty"/> for an error about the entity as a whole. A key can carry several messages.
    /// </summary>
    /// <param name="key">The property path, or <see cref="string.Empty"/>.</param>
    /// <param name="message">
    /// What the client shows — a text, or a translation key a client pairs with its own messages. See
    /// <see cref="EntityInputError.Message"/>.
    /// </param>
    /// <param name="args">
    /// The values a translation fills in: an anonymous object (<c>new { max = 50 }</c>) or a dictionary keyed by
    /// placeholder name. See <see cref="EntityInputError.Args"/>.
    /// </param>
    void AddError(string key, string message, object? args = null);
}

/// <inheritdoc cref="IEntityValidatorContext"/>
/// <typeparam name="TEntity">
/// The entity type, or any type it derives from or implements — the context of an <c>Order</c> is also an
/// <c>IEntityValidatorContext&lt;IHasTenantId&gt;</c>.
/// </typeparam>
public interface IEntityValidatorContext<out TEntity> : IEntityValidatorContext
{
    /// <inheritdoc cref="IEntityValidatorContext.Item"/>
    new TEntity Item { get; }
    /// <inheritdoc cref="IEntityValidatorContext.Original"/>
    new TEntity? Original { get; }
}
