namespace Regira.Entities.Models;

/// <summary>
/// One reason a write was rejected.
/// </summary>
/// <param name="Key">
/// The property path (<c>CustomerId</c>, <c>Lines[0].Quantity</c>), or <see cref="string.Empty"/> for the entity as a whole.
/// </param>
/// <param name="Message">
/// What the client shows: a text (<c>A code is required.</c>), or a translation key (<c>ValueTooLarge</c>) a client that
/// translates pairs with its own messages.
/// </param>
/// <param name="Args">
/// The values a translation fills in, by placeholder name (<c>{max}</c>). Only scalar values — text, numbers, booleans,
/// dates, times, <see cref="Guid"/>s and enums — reach the client.
/// </param>
public record EntityInputError(string Key, string Message, IReadOnlyDictionary<string, object?>? Args = null);
