using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// The concurrency-token values a client sent, read from the incoming entity before anything on the write path
/// could change them.
/// </summary>
internal sealed record ClientConcurrencyTokens(IReadOnlyList<(IProperty Property, object? Value)> Values)
{
    public static ClientConcurrencyTokens None { get; } = new(Array.Empty<(IProperty, object?)>());
}

/// <summary>
/// Makes EF Core's optimistic concurrency check compare the database with what the <b>client</b> last read.
/// <para>
/// An update reloads the stored row and copies it into the incoming entity's original values
/// (<c>OriginalValues.SetValues(stored)</c>) — which is where primers read a stored value from. Left at that, every
/// concurrency token's original value is the database's own, and <c>UPDATE … WHERE token = @original</c> compares
/// the database with itself: a stale client always passes, and only a write landing between the reload and
/// <c>SaveChanges</c> is caught. Putting the client's value back as the original is the whole check.
/// </para>
/// <para>
/// Shared by every place the write path attaches an update — the entity itself, owned children synced by
/// <c>Related()</c>, attachment join rows — and by the startup validator, so the runtime and the diagnostic agree
/// on what a token is and on when the client supplied one.
/// </para>
/// </summary>
internal static class ConcurrencyTokenExtensions
{
    /// <summary>
    /// The concurrency tokens a client can round-trip: every property the model marks as one — <c>[Timestamp]</c> /
    /// <c>IsRowVersion()</c>, <c>[ConcurrencyCheck]</c>, <c>IsConcurrencyToken()</c> — except shadow properties (no
    /// CLR member a DTO could map to) and key properties (the route owns those).
    /// </summary>
    internal static IEnumerable<IProperty> GetClientConcurrencyTokens(this IEntityType entityType)
        => entityType.GetProperties().Where(p => p.IsConcurrencyToken && !p.IsShadowProperty() && !p.IsKey());

    /// <summary>
    /// Reads the client's value of every concurrency token of <paramref name="incoming"/>'s entity type. Call it
    /// before anything on the write path runs: a prepper may overwrite the value — <c>[ServerOwned]</c> restores it
    /// from the stored row.
    /// </summary>
    internal static ClientConcurrencyTokens CaptureClientTokens(this DbContext dbContext, object incoming)
    {
        var entityType = dbContext.Model.FindRuntimeEntityType(incoming.GetType());
        if (entityType == null)
        {
            return ClientConcurrencyTokens.None;
        }

        var values = entityType.GetClientConcurrencyTokens()
            .Select(property => (property, ReadClrValue(property, incoming)))
            .ToArray();
        return values.Length == 0 ? ClientConcurrencyTokens.None : new ClientConcurrencyTokens(values);
    }

    /// <summary>
    /// Tracks <paramref name="incoming"/> as the update of <paramref name="stored"/> — the stored row becomes its
    /// original values and every property is marked modified — then gives each supplied concurrency token the
    /// client's value back as its original.
    /// <para>
    /// A token the client left out (see <see cref="IsSupplied"/>) keeps the stored value as its original, so that
    /// request is checked only against a write racing this save. Its current value goes back to the stored one while
    /// it still holds what the client sent, so the placeholder is never written over a real token.
    /// </para>
    /// </summary>
    internal static EntityEntry TrackAsUpdateOf(this DbContext dbContext, object incoming, object stored, ClientConcurrencyTokens clientTokens)
    {
        dbContext.Entry(stored).State = EntityState.Detached;
        dbContext.Attach(incoming);
        var entry = dbContext.Entry(incoming);
        entry.OriginalValues.SetValues(stored);
        entry.State = EntityState.Modified;

        // after the state change, so nothing it does can touch the originals set here
        foreach (var (property, clientValue) in clientTokens.Values)
        {
            var token = entry.Property(property);
            if (IsSupplied(property, clientValue))
            {
                token.OriginalValue = clientValue;
            }
            else if (property.GetValueComparer().Equals(token.CurrentValue, clientValue))
            {
                token.CurrentValue = token.OriginalValue;
            }
        }

        return entry;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a token the client sent rather than a placeholder for "not sent":
    /// <c>null</c>, an empty <c>byte[]</c> or string, and the property's <c>Sentinel</c> (its CLR default unless
    /// configured with <c>HasSentinel</c>) all count as absent. A token whose default is a legitimate value — an
    /// <c>int</c> version that starts at 0 — therefore cannot be told apart from an absent one.
    /// </summary>
    internal static bool IsSupplied(IProperty property, object? value)
        => value switch
        {
            null => false,
            byte[] { Length: 0 } => false,
            string { Length: 0 } => false,
            _ => !property.GetValueComparer().Equals(value, property.Sentinel)
        };

    /// <summary>
    /// The CLR value of <paramref name="property"/> on an entity that need not be tracked. Reflection rather than
    /// EF's own getter, whose API differs between the EF Core majors this package targets.
    /// </summary>
    internal static object? ReadClrValue(IProperty property, object entity)
        => property.PropertyInfo?.GetMethod != null
            ? property.PropertyInfo.GetValue(entity)
            : property.FieldInfo?.GetValue(entity);
}
