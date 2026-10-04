using FluentValidation;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.Validation.FluentValidation;

/// <summary>What the entity write pipeline passes to a FluentValidation validator, read from any rule's context.</summary>
public static class ValidationContextExtensions
{
    internal const string OriginalKey = "Regira.Entities.Original";
    internal const string OperationKey = "Regira.Entities.Operation";

    /// <summary>
    /// The row as stored before the write, for <see cref="EntityWriteOperation.Modify"/>; <c>null</c> for an insert or a
    /// delete, and in a child validator whose <typeparamref name="T"/> is not the entity's type.
    /// </summary>
    public static T? GetOriginal<T>(this ValidationContext<T> context)
        => context.RootContextData.TryGetValue(OriginalKey, out var original) && original is T typed ? typed : default;

    /// <summary>The write being checked; <c>null</c> when the validator was not run by the entity write pipeline.</summary>
    public static EntityWriteOperation? GetOperation<T>(this ValidationContext<T> context)
        => context.RootContextData.TryGetValue(OperationKey, out var operation) && operation is EntityWriteOperation typed ? typed : null;
}
