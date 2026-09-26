using System.Linq.Expressions;
using System.Reflection;

namespace Regira.Entities.Reactors.Abstractions;

public static class EntityChangeExtensions
{
    /// <summary>
    /// Whether the save changed <paramref name="property"/> on a stored row: <see cref="EntityChangeKind.Modified"/> with a
    /// committed value that differs from the stored one. Selecting a complex property matches a change to any of its members.
    /// Always <c>false</c> for <see cref="EntityChangeKind.Added"/> and <see cref="EntityChangeKind.Deleted"/>.
    /// </summary>
    /// <param name="change">The committed change.</param>
    /// <param name="property">The property, e.g. <c>x =&gt; x.Status</c> or <c>x =&gt; x.Address.City</c>.</param>
    public static bool HasChanged<TEntity, TProp>(this IEntityChange<TEntity> change, Expression<Func<TEntity, TProp>> property)
    {
        var path = string.Join(".", MemberPath.Of(property).Select(p => p.Name));
        return change.ChangedProperties.Contains(path)
               || change.ChangedProperties.Any(p => p.StartsWith(path + ".", StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether the save brought <paramref name="property"/> to <paramref name="value"/>: an inserted row holding it, or a
    /// stored row that held another value. A save that leaves the value as it was — or deletes the row — does not count.
    /// </summary>
    /// <param name="change">The committed change.</param>
    /// <param name="property">The property, e.g. <c>x =&gt; x.Status</c>.</param>
    /// <param name="value">The value the property must have been changed to.</param>
    public static bool ChangedTo<TEntity, TProp>(this IEntityChange<TEntity> change, Expression<Func<TEntity, TProp>> property, TProp value)
    {
        if (change.Kind == EntityChangeKind.Deleted)
        {
            return false;
        }

        var path = MemberPath.Of(property);
        var comparer = EqualityComparer<TProp>.Default;
        if (!comparer.Equals(MemberPath.Read<TProp>(change.Entity, path), value))
        {
            return false;
        }
        return change.Kind == EntityChangeKind.Added || !comparer.Equals(MemberPath.Read<TProp>(change.Original, path), value);
    }
}

/// <summary>Reads a property chain (<c>x =&gt; x.Address.City</c>) from a selector without compiling it.</summary>
internal static class MemberPath
{
    public static IReadOnlyList<PropertyInfo> Of(LambdaExpression selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            body = convert.Operand;
        }

        var chain = new List<PropertyInfo>();
        while (body is MemberExpression { Member: PropertyInfo property } member)
        {
            chain.Insert(0, property);
            body = member.Expression;
        }
        if (chain.Count == 0 || body != selector.Parameters[0])
        {
            throw new ArgumentException($"'{selector}' must select a property of the entity, e.g. x => x.Status or x => x.Address.City.", nameof(selector));
        }
        return chain;
    }

    public static TProp? Read<TProp>(object? source, IReadOnlyList<PropertyInfo> path)
    {
        var value = source;
        foreach (var property in path)
        {
            if (value == null)
            {
                return default;
            }
            value = property.GetValue(value);
        }
        return value is TProp typed ? typed : default;
    }
}
