using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Reads the stored rows of entries whose original values are not known to be them: what EF's
/// <c>GetDatabaseValues</c> reads for one entry, in one keyed query per entity type, so an <c>Update(detached)</c> of
/// twenty rows is one read rather than twenty. Like <c>GetDatabaseValues</c>, it reads past query filters and tracks
/// nothing. A lone row, a row the query did not return, and an entity type a keyed <c>IN</c> query does not cover — an
/// owned or shared-type entity, a composite or converted key, a complex collection or an optional complex property —
/// are read by <c>GetDatabaseValues</c> itself.
/// </summary>
internal static class StoredRowReader
{
    // keeps the key list well under the parameter limits of the providers that expand it into one parameter per key
    private const int BatchSize = 500;

    /// <summary>The stored row of each entry; <c>null</c> for a row that is no longer stored.</summary>
    public static async Task<IReadOnlyDictionary<EntityEntry, PropertyValues?>> Read(DbContext dbContext, IReadOnlyCollection<EntityEntry> entries,
        bool async, CancellationToken token)
    {
        var stored = new Dictionary<EntityEntry, PropertyValues?>(ReferenceEqualityComparer.Instance);
        foreach (var group in entries.GroupBy(e => e.Metadata))
        {
            var sameType = group.ToArray();
            if (sameType.Length > 1 && KeyedQuery.For(group.Key) is { } query)
            {
                await query.Read(dbContext, sameType, stored, async, token);
            }
            foreach (var entry in sameType.Where(e => !stored.ContainsKey(e)))
            {
                stored[entry] = async ? await entry.GetDatabaseValuesAsync(token) : entry.GetDatabaseValues();
            }
        }
        return stored;
    }

    /// <summary>
    /// The query for one entity type, built once: it selects every stored value — shadow properties and complex
    /// members included, in the order of <see cref="_properties"/> — of the rows whose single key is in a list.
    /// </summary>
    private sealed class KeyedQuery
    {
        private delegate Task<List<object?[]>> Runner(DbContext dbContext, IReadOnlyList<object> keys, string keyName, LambdaExpression projection,
            bool async, CancellationToken token);

        private static readonly ConditionalWeakTable<IEntityType, KeyedQuery?> Queries = new();
        private static readonly MethodInfo RunMethod = typeof(KeyedQuery).GetMethod(nameof(Run), BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo PropertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!;

        private readonly IProperty[] _properties;
        private readonly int _keyIndex;
        private readonly LambdaExpression _projection;
        private readonly Runner _run;

        private KeyedQuery(IEntityType entityType, IProperty key)
        {
            _properties = FlattenedProperties(entityType).ToArray();
            _keyIndex = Array.IndexOf(_properties, key);
            _projection = Projection(entityType.ClrType, _properties);
            _run = RunMethod.MakeGenericMethod(entityType.ClrType, key.ClrType).CreateDelegate<Runner>();
        }

        public static KeyedQuery? For(IEntityType entityType) => Queries.GetValue(entityType, Build);

        private static KeyedQuery? Build(IEntityType entityType)
        {
            var key = entityType.FindPrimaryKey();
            if (key is not { Properties.Count: 1 } || key.Properties[0].GetValueConverter() != null
                || entityType.IsOwned() || entityType.HasSharedClrType || !IsFlat(entityType))
            {
                return null;
            }
            return new KeyedQuery(entityType, key.Properties[0]);
        }

        public async Task Read(DbContext dbContext, EntityEntry[] entries, Dictionary<EntityEntry, PropertyValues?> stored, bool async, CancellationToken token)
        {
            var key = _properties[_keyIndex];
            var byKey = new Dictionary<object, EntityEntry>();
            foreach (var entry in entries)
            {
                if (entry.CurrentValues[key] is { } value)
                {
                    byKey.TryAdd(value, entry);
                }
            }

            foreach (var keys in byKey.Keys.Chunk(BatchSize))
            {
                foreach (var row in await _run(dbContext, keys, key.Name, _projection, async, token))
                {
                    if (row[_keyIndex] is not { } value || !byKey.TryGetValue(value, out var entry))
                    {
                        continue;
                    }
                    // the entry's own originals give the shape; every value is then the stored one
                    var values = entry.OriginalValues.Clone();
                    for (var i = 0; i < _properties.Length; i++)
                    {
                        values[_properties[i]] = row[i];
                    }
                    stored[entry] = values;
                }
            }
        }

        private static async Task<List<object?[]>> Run<TEntity, TKey>(DbContext dbContext, IReadOnlyList<object> keys, string keyName,
            LambdaExpression projection, bool async, CancellationToken token)
            where TEntity : class
        {
            var typedKeys = keys.Cast<TKey>().ToList();
            var query = dbContext.Set<TEntity>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(e => typedKeys.Contains(EF.Property<TKey>(e, keyName)))
                .Select((Expression<Func<TEntity, object?[]>>)projection);
            return async ? await query.ToListAsync(token) : query.ToList();
        }

        // e => new object[] { EF.Property<int>(e, "Id"), ..., EF.Property<string>(EF.Property<Address>(e, "Address"), "City") }
        private static LambdaExpression Projection(Type clrType, IProperty[] properties)
        {
            var entity = Expression.Parameter(clrType, "e");
            var values = properties.Select(property =>
            {
                Expression instance = entity;
                foreach (var complexProperty in ComplexPathOf(property))
                {
                    instance = Expression.Call(PropertyMethod.MakeGenericMethod(complexProperty.ClrType), instance, Expression.Constant(complexProperty.Name));
                }
                var value = Expression.Call(PropertyMethod.MakeGenericMethod(property.ClrType), instance, Expression.Constant(property.Name));
                return (Expression)Expression.Convert(value, typeof(object));
            });
            return Expression.Lambda(typeof(Func<,>).MakeGenericType(clrType, typeof(object?[])), Expression.NewArrayInit(typeof(object), values), entity);
        }

        private static IEnumerable<IProperty> FlattenedProperties(ITypeBase type)
            => type.GetProperties().Concat(type.GetComplexProperties().SelectMany(c => FlattenedProperties(c.ComplexType)));

        private static IEnumerable<IComplexProperty> ComplexPathOf(IProperty property)
        {
            var path = new List<IComplexProperty>();
            for (var type = property.DeclaringType; type is IComplexType complexType; type = complexType.ComplexProperty.DeclaringType)
            {
                path.Insert(0, complexType.ComplexProperty);
            }
            return path;
        }

        // every complex property is a single, required value — nothing the flat projection would have to guess at
        private static bool IsFlat(ITypeBase type)
            => type.GetComplexProperties().All(c => !c.IsCollection && !c.IsNullable && IsFlat(c.ComplexType));
    }
}
