using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Regira.Entities.Attributes;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;

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
/// That holds for a <b>version stamp</b> only — a token the server sets on the write, which the client echoes and
/// never edits. EF's <c>[ConcurrencyCheck]</c> also serves on a data column the client edits
/// (<c>[ConcurrencyCheck] public string LastName</c>); there the client's value is the new data, not a claim about
/// what it read, and making it the original would refuse every change and undo every clear. So a token counts as a
/// version stamp when the database generates it on update, when it is
/// <see cref="IHasConcurrencyToken.ConcurrencyToken"/>, when it carries <see cref="VersionStampAttribute"/>, or when
/// the write path moves it — a prepper before the entity is attached, a primer during the save. Any other token is a
/// data column: it keeps the stored value as its original and the client's value as the one written, so only a write
/// racing the save is caught. The last rule judges one write at a time: a primer that produces the value the client
/// sent leaves an undeclared token looking like a data column, which is what the attribute is for.
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
    /// Tokens whose kind only the save can tell, per context and entity: a primer that moves one makes it a version
    /// stamp. Weak on the context, so a context that is never saved takes them with it.
    /// <para>
    /// State the change tracker cannot hold for us, so its lifecycle is spelled out: an entry is added by
    /// <see cref="TrackAsUpdateOf"/> and removed by <see cref="ApplyUndecidedClientTokens"/>, which the primer
    /// interceptor calls on every save and <c>ApplyPrimers</c> on demand. A context wired without the primer
    /// interceptor and never primed by hand keeps its entries — and the entities they reference — until it is
    /// collected; a pooled context keeps them until its next save, which drops any entry whose entity is no longer
    /// tracked. Nothing here outlives the context, and nothing is keyed on the entity's identity, so a fresh entity
    /// of the next request can never inherit a decision.
    /// </para>
    /// </summary>
    private static readonly ConditionalWeakTable<DbContext, Dictionary<object, ClientConcurrencyTokens>> Undecided = new();

    /// <summary>
    /// The concurrency tokens a client can round-trip: every property the model marks as one — <c>[Timestamp]</c> /
    /// <c>IsRowVersion()</c>, <c>[ConcurrencyCheck]</c>, <c>IsConcurrencyToken()</c> — except shadow properties (no
    /// CLR member a DTO could map to) and key properties (the route owns those).
    /// </summary>
    internal static IEnumerable<IProperty> GetClientConcurrencyTokens(this IEntityType entityType)
        => entityType.GetProperties().Where(p => p.IsConcurrencyToken && !p.IsShadowProperty() && !p.IsKey());

    /// <summary>
    /// Whether the model alone says <paramref name="token"/> is a version stamp: the database generates it on update
    /// (<c>[Timestamp]</c>, <c>IsRowVersion()</c>, a computed column), it is
    /// <see cref="IHasConcurrencyToken.ConcurrencyToken"/>, or it carries <see cref="VersionStampAttribute"/>. Any other
    /// token is a version stamp only on a write that moves it, which the model cannot tell.
    /// </summary>
    /// <param name="token">The concurrency token.</param>
    /// <param name="entityType">The entity the token is read from — a derived type may carry the marker that its
    /// mapped base, which declares the property, does not.</param>
    internal static bool IsDeclaredVersionStamp(this IProperty token, Type entityType)
        => token.IsStoreGeneratedOnUpdate()
           || (token.Name == nameof(IHasConcurrencyToken.ConcurrencyToken)
               && typeof(IHasConcurrencyToken).IsAssignableFrom(entityType))
           || token.VersionStamp() != null;

    /// <summary>
    /// Whether the store moves <paramref name="property"/> on an <c>UPDATE</c>: a rowversion (<c>[Timestamp]</c>,
    /// <c>IsRowVersion()</c>), or a computed column, which may change on any update. Such a token is a version stamp
    /// by construction, and the only kind whose stored value is safe to re-read after a write of one's own — an
    /// application-owned token never moves on its own, so re-reading it would adopt another writer's value.
    /// </summary>
    internal static bool IsStoreGeneratedOnUpdate(this IProperty property)
        => (property.ValueGenerated & ValueGenerated.OnUpdate) != 0
           || property.ValueGenerated == ValueGenerated.OnUpdateSometimes;

    /// <summary>
    /// The <see cref="VersionStampAttribute"/> on <paramref name="token"/>'s CLR property, inherited ones included;
    /// <c>null</c> when it carries none.
    /// </summary>
    internal static VersionStampAttribute? VersionStamp(this IProperty token)
        => token.PropertyInfo?.GetCustomAttribute<VersionStampAttribute>(inherit: true);

    /// <summary>
    /// Whether an update must carry <paramref name="token"/>: <c>[VersionStamp(Required = true)]</c>. Only a declared
    /// stamp can be required — the attribute declares it — so the rule never touches a data column.
    /// </summary>
    internal static bool IsRequiredVersionStamp(this IProperty token)
        => token.VersionStamp()?.Required == true;

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
    /// original values and every property is marked modified — then gives each supplied version stamp the client's
    /// value back as its original.
    /// <para>
    /// A version stamp the client left out (see <see cref="IsSupplied"/>) keeps the stored value as its original, so
    /// that request is checked only against a write racing this save. Its current value goes back to the stored one
    /// while it still holds what the client sent, so the placeholder is never written over a real token.
    /// </para>
    /// <para>
    /// A token nothing has moved yet and the model does not declare a version stamp is left as it is, and decided by
    /// <see cref="ApplyUndecidedClientTokens"/> once the primers have run.
    /// </para>
    /// </summary>
    /// <exception cref="EntityInputException{T}">A required version stamp (<see cref="IsRequiredVersionStamp"/>) the
    /// client left out. Thrown before anything is attached, so the tracker is as the caller left it.</exception>
    internal static EntityEntry TrackAsUpdateOf<TEntity>(this DbContext dbContext, TEntity incoming, TEntity stored, ClientConcurrencyTokens clientTokens)
        where TEntity : class
    {
        RequireSuppliedStamps(incoming, clientTokens);

        dbContext.Entry(stored).State = EntityState.Detached;
        dbContext.Attach(incoming);
        var entry = dbContext.Entry(incoming);
        entry.OriginalValues.SetValues(stored);
        entry.State = EntityState.Modified;

        // after the state change, so nothing it does can touch the originals set here
        var undecided = new List<(IProperty, object?)>();
        foreach (var (property, clientValue) in clientTokens.Values)
        {
            var token = entry.Property(property);
            var moved = !property.GetValueComparer().Equals(token.CurrentValue, clientValue);
            if (!moved && !property.IsDeclaredVersionStamp(incoming.GetType()))
            {
                undecided.Add((property, clientValue));
            }
            else if (IsSupplied(property, clientValue))
            {
                token.OriginalValue = clientValue;
            }
            else if (!moved)
            {
                token.CurrentValue = token.OriginalValue;
            }
        }

        if (undecided.Count > 0)
        {
            // by reference: an entity may define equality by key, and a stored twin must not stand in for it
            Undecided.GetValue(dbContext, _ => new Dictionary<object, ClientConcurrencyTokens>(ReferenceEqualityComparer.Instance))[incoming]
                = new ClientConcurrencyTokens(undecided);
        }
        else if (Undecided.TryGetValue(dbContext, out var pending))
        {
            pending.Remove(incoming);
        }

        return entry;
    }

    /// <summary>
    /// Refuses the update when a required version stamp was not supplied, naming every such stamp as a field error —
    /// the shape the web layers return as a 400 — with the incoming entity as the exception's item.
    /// </summary>
    private static void RequireSuppliedStamps<TEntity>(TEntity incoming, ClientConcurrencyTokens clientTokens)
        where TEntity : class
    {
        var missing = clientTokens.Values
            .Where(t => t.Property.IsRequiredVersionStamp() && !IsSupplied(t.Property, t.Value))
            .Select(t => t.Property.Name)
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        var exception = new EntityInputException<TEntity>(
            $"{typeof(TEntity).Name} requires {string.Join(", ", missing)} on an update: send the value read with the record.")
        {
            Item = incoming
        };
        foreach (var name in missing)
        {
            exception.InputErrors[name] = "Required on an update: send the value read with the record.";
        }
        throw exception;
    }

    /// <summary>
    /// Decides the tokens <see cref="TrackAsUpdateOf"/> left open; the primer interceptor calls it once every primer
    /// ran. A token something moved away from the client's value is a version stamp, and a supplied one gets the
    /// client's value as its original. A token nothing moved is a data column: it keeps the stored value as its
    /// original and the client's value — empty or not — as the one written.
    /// </summary>
    /// <param name="dbContext">The context about to save.</param>
    /// <param name="entityType">Only the entities of this type or a subtype — the ones the primers just ran for.</param>
    internal static void ApplyUndecidedClientTokens(this DbContext dbContext, Type? entityType = null)
    {
        if (!Undecided.TryGetValue(dbContext, out var pending))
        {
            return;
        }

        var decided = pending.Where(p => entityType?.IsInstanceOfType(p.Key) ?? true).ToArray();
        foreach (var (incoming, clientTokens) in decided)
        {
            pending.Remove(incoming);
            var entry = dbContext.Entry(incoming);
            if (entry.State != EntityState.Modified)
            {
                continue;
            }
            foreach (var (property, clientValue) in clientTokens.Values)
            {
                var token = entry.Property(property);
                if (IsSupplied(property, clientValue) && !property.GetValueComparer().Equals(token.CurrentValue, clientValue))
                {
                    token.OriginalValue = clientValue;
                }
            }
        }
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
