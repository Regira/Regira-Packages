using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Reactors;
using Regira.Entities.Reactors.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// A row a save is about to write, captured in <c>SavingChanges</c> — after the primers, before the database is touched.
/// The stored values and the columns an update writes are taken here: both are gone once the change is accepted. The
/// written values are read after the save, when store-generated keys and values are known.
/// </summary>
internal sealed class CapturedChange(EntityEntry entry, EntityChangeKind kind, PropertyValues? stored, IReadOnlySet<IProperty> written)
{
    private static readonly IReadOnlySet<IProperty> NoneWritten = new HashSet<IProperty>();

    /// <summary>
    /// Captures the pending <paramref name="entries"/>. An update or delete is reported against the stored row: the
    /// entry's own originals when it is marked as holding it (<see cref="StoredOriginalsExtensions"/>), otherwise the
    /// row read now — in one query per entity type.
    /// </summary>
    public static async Task<List<CapturedChange>> CaptureAll(DbContext dbContext, IReadOnlyCollection<EntityEntry> entries, bool async, CancellationToken token)
    {
        var unknown = entries.Where(e => e.State != EntityState.Added && !e.HasStoredOriginals()).ToArray();
        var read = await StoredRowReader.Read(dbContext, unknown, async, token);
        return entries.Select(entry => Capture(entry, read.GetValueOrDefault(entry))).ToList();
    }

    private static CapturedChange Capture(EntityEntry entry, PropertyValues? stored)
    {
        var kind = entry.State switch
        {
            EntityState.Added => EntityChangeKind.Added,
            EntityState.Deleted => EntityChangeKind.Deleted,
            _ => EntityChangeKind.Modified
        };
        if (kind == EntityChangeKind.Added)
        {
            return new CapturedChange(entry, kind, null, NoneWritten);
        }
        // a copy: the entry's own originals are accepted (overwritten) by the save. A row that is no longer stored
        // leaves nothing better than what the entry holds — the save will fail on it anyway.
        return new CapturedChange(entry, kind, stored ?? entry.OriginalValues.Clone(),
            kind == EntityChangeKind.Modified ? WrittenProperties(entry) : NoneWritten);
    }

    // the columns the update writes: flagged now, after the primers, which is what the save sends
    private static HashSet<IProperty> WrittenProperties(EntityEntry entry)
    {
        var written = new HashSet<IProperty>(entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata));
        AddWritten(entry.ComplexProperties, written);
        return written;
    }

    private static void AddWritten(IEnumerable<ComplexPropertyEntry> complexProperties, HashSet<IProperty> written)
    {
        foreach (var complexProperty in complexProperties)
        {
            written.UnionWith(complexProperty.Properties.Where(p => p.IsModified).Select(p => p.Metadata));
            AddWritten(complexProperty.ComplexProperties, written);
        }
    }

    /// <summary>Builds the change a reactor receives. Call it after the save, before the entry is used again.</summary>
    public IEntityChange ToChange()
    {
        var entityType = entry.Metadata.ClrType;
        if (kind == EntityChangeKind.Deleted)
        {
            // the row as it was stored: a stub delete carries nothing but its key
            return EntityChangeFactory.Create(entityType, kind, stored!.ToObject(), stored.ToObject(), []);
        }

        var current = entry.CurrentValues;
        if (kind == EntityChangeKind.Added)
        {
            return EntityChangeFactory.Create(entityType, kind, current.ToObject(), null, []);
        }

        // The committed row is the stored one with what the update wrote: a column it left alone kept its stored value,
        // whatever the entry holds — a soft-deleted stub's empty values were never written.
        var committed = stored!.Clone();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in current.Properties)
        {
            if (written.Contains(property) || property.IsStoreGeneratedOnUpdate())
            {
                committed[property] = current[property];
            }
            if (!property.GetValueComparer().Equals(stored[property], committed[property]))
            {
                changed.Add(PathOf(property));
            }
        }
        return EntityChangeFactory.Create(entityType, kind, committed.ToObject(), stored.ToObject(), changed);
    }

    // a complex type's member as the dotted path a selector (x => x.Address.City) spells
    private static string PathOf(IPropertyBase property)
        => property.DeclaringType is IComplexType complexType
            ? $"{PathOf(complexType.ComplexProperty)}.{property.Name}"
            : property.Name;
}

/// <summary>Creates an <see cref="EntityChange{TEntity}"/> closed over the entity's runtime type.</summary>
internal static class EntityChangeFactory
{
    private delegate IEntityChange Factory(EntityChangeKind kind, object entity, object? original, IReadOnlyCollection<string> changedProperties);

    private static readonly ConcurrentDictionary<Type, Factory> Factories = new();
    private static readonly MethodInfo CreateTypedMethod = typeof(EntityChangeFactory).GetMethod(nameof(CreateTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static IEntityChange Create(Type entityType, EntityChangeKind kind, object entity, object? original, IReadOnlyCollection<string> changedProperties)
        => Factories.GetOrAdd(entityType, type => CreateTypedMethod.MakeGenericMethod(type).CreateDelegate<Factory>())
            (kind, entity, original, changedProperties);

    private static IEntityChange CreateTyped<TEntity>(EntityChangeKind kind, object entity, object? original, IReadOnlyCollection<string> changedProperties)
        where TEntity : class
        => new EntityChange<TEntity>(kind, (TEntity)entity, (TEntity?)original, changedProperties);
}
