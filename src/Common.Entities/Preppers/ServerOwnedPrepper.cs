using Regira.Entities.Attributes;
using Regira.Entities.Preppers.Abstractions;
using System.Linq.Expressions;
using System.Reflection;

namespace Regira.Entities.Preppers;

/// <summary>
/// Declares one scalar property (or foreign key) as owned by the server — the fluent counterpart of
/// <see cref="ServerOwnedAttribute"/>, with the create-time mint the attribute cannot carry: minted by
/// <c>mintOnCreate</c> when still unset on create, restored from the stored row on update.
/// <para>
/// A prepper, so it guards the <c>IEntityService</c> write path and leaves a domain/workflow service's raw
/// <c>DbContext</c> write alone — where a primer restoring from <c>entry.OriginalValues</c> would revert it.
/// </para>
/// </summary>
public class ServerOwnedPrepper<TEntity, TProp> : EntityPrepperBase<TEntity>
    where TEntity : class
{
    private readonly Func<TEntity, TProp> _get;
    private readonly Action<TEntity, TProp> _set;
    private readonly Func<TEntity, TProp>? _mintOnCreate;

    /// <param name="selector">The property to protect, e.g. <c>x =&gt; x.Code</c>.</param>
    /// <param name="mintOnCreate">Mints the value on create when the property is unset. Omit to protect only.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="selector"/> is not a property selector, or the property cannot be server-owned
    /// (see <see cref="ServerOwnedProperties.SkipReason"/>).
    /// </exception>
    public ServerOwnedPrepper(Expression<Func<TEntity, TProp>> selector, Func<TEntity, TProp>? mintOnCreate = null)
    {
        if (selector.Body is not MemberExpression { Member: PropertyInfo property })
        {
            throw new ArgumentException("Expected a property selector, e.g. x => x.Code.", nameof(selector));
        }
        if (ServerOwnedProperties.SkipReason(typeof(TEntity), property) is { } reason)
        {
            throw new ArgumentException($"{typeof(TEntity).Name}.{property.Name} cannot be server-owned: {reason}.", nameof(selector));
        }

        _get = selector.Compile();
        _set = BuildSetter(property);
        _mintOnCreate = mintOnCreate;
    }

    public override Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default)
    {
        if (original == null)
        {
            if (_mintOnCreate != null && IsUnset(_get(modified)))
            {
                _set(modified, _mintOnCreate(modified));
            }
        }
        else
        {
            _set(modified, _get(original));
        }

        return Task.CompletedTask;
    }

    private static bool IsUnset(TProp value)
        => value is string text
            ? string.IsNullOrWhiteSpace(text)
            : EqualityComparer<TProp>.Default.Equals(value, default!);

    private static Action<TEntity, TProp> BuildSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(TEntity), "e");
        var value = Expression.Parameter(typeof(TProp), "v");
        var body = Expression.Assign(Expression.Property(target, property), value);
        return Expression.Lambda<Action<TEntity, TProp>>(body, target, value).Compile();
    }
}
