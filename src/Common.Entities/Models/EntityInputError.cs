namespace Regira.Entities.Models;

/// <summary>
/// One reason a write was rejected: <paramref name="Key"/> is the property path (<c>CustomerId</c>,
/// <c>Lines[0].Quantity</c>), or <see cref="string.Empty"/> for the entity as a whole.
/// </summary>
public record EntityInputError(string Key, string Message);
