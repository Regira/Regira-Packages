namespace Regira.Entities.Models.Abstractions;

/// <summary>
/// Marks an entity as having a short display name.
/// </summary>
/// <remarks>
/// <c>Title</c> is declared as <c>string?</c> as a contract convention — the interface itself does not
/// enforce a value. If your domain requires a non-null title, add <c>[Required]</c> to the implementing
/// property; the <c>string?</c> type declaration remains unchanged. Use the auto-truncate and normaliser
/// interceptors to handle whitespace. Implementing entities declare the property as <c>{ get; set; }</c> —
/// C# allows widening a getter-only interface property with a setter, and persistence needs it writable.
/// </remarks>
public interface IHasTitle
{
    public string? Title { get; }
}

/// <summary>
/// Extends <see cref="IHasTitle"/> with a writable normalised version of the title, suitable for
/// case-insensitive search.
/// </summary>
public interface IHasNormalizedTitle : IHasTitle
{
    string? NormalizedTitle { get; set; }
}