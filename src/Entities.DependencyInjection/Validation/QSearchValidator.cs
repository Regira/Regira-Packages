using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Warns for entities where a <c>?q=</c> search will be silently ignored: the entity does not implement
/// <see cref="IHasNormalizedContent"/> (so the built-in Q filter never applies), and either no custom
/// <c>IFilteredQueryBuilder</c> is registered that could consume the search object, or its custom filters never read
/// <see cref="ISearchObject.Q"/>.
/// <para>
/// The second case is found by building the custom filters' query on an empty in-memory source twice, once without
/// <c>Q</c> and once with a probe value, with the values the query captured written out: when both read the same,
/// nothing in the filters depends on <c>Q</c>. A filter that cannot be built that way (a search object without a
/// parameterless constructor, a filter that throws) counts as reading it, so the check stays silent rather than guess.
/// An entity one of Regira's own filters serves (an attachment, an attachment link) is not checked: those search their
/// own fields and leave <c>Q</c> alone by design, which the app cannot change.
/// </para>
/// </summary>
internal sealed class QSearchValidator : IEntityRegistrationValidator
{
    private const string QProbe = "regira-q-probe";
    private static readonly MethodInfo ChangesWithQMethod =
        typeof(QSearchValidator).GetMethod(nameof(ChangesWithQ), BindingFlags.NonPublic | BindingFlags.Static)!;

    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var candidates = context.Registrations.Entities
            .Select(r => r.EntityType)
            .Where(t => !typeof(IHasNormalizedContent).IsAssignableFrom(t))
            .Distinct()
            .Select(t => (EntityType: t, Filters: CustomFilterServiceTypes(context, t)))
            .ToArray();

        var unsearchable = candidates
            .Where(c => c.Filters.Length == 0)
            .Select(c => c.EntityType.Name)
            .ToArray();
        if (unsearchable.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"?q= text search is silently ignored for: {string.Join(", ", unsearchable)}. " +
                "These entities do not implement IHasNormalizedContent and have no custom IFilteredQueryBuilder — " +
                "a q parameter returns unfiltered results. Implement IHasNormalizedContent (with a [Normalized] source) or register a custom filter." +
                " An entity whose own IEntityService or IEntityRepository applies Q can ignore this.");
        }

        var qIgnored = candidates
            .Where(c => c.Filters.Length > 0 && !c.Filters.Any(f => ReadsQ(context.Provider, f)))
            .Select(c => c.EntityType.Name)
            .ToArray();
        if (qIgnored.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"?q= text search is ignored by the custom filters of: {string.Join(", ", qIgnored)}. " +
                "These entities do not implement IHasNormalizedContent, and their filters build the same query with and without Q — " +
                "a q parameter returns unfiltered results. Read so.Q in the filter (FilterQ searches the columns you name) or implement IHasNormalizedContent." +
                " An entity whose own IEntityService or IEntityRepository applies Q can ignore this.");
        }
    }

    private static Type[] CustomFilterServiceTypes(EntityValidationContext context, Type entityType)
        => context.Services
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType
                && t.GetGenericTypeDefinition() == typeof(IFilteredQueryBuilder<,,>)
                && t.GetGenericArguments()[0] == entityType)
            .Distinct()
            .ToArray();

    private static bool ReadsQ(IServiceProvider provider, Type filterServiceType)
    {
        try
        {
            return (bool)ChangesWithQMethod.MakeGenericMethod(filterServiceType.GetGenericArguments()).Invoke(null, [provider])!;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Whether the registered filters, applied in turn as the query builder does, build another query once <c>Q</c> is set.</summary>
    private static bool ChangesWithQ<TEntity, TKey, TSearchObject>(IServiceProvider provider)
        where TSearchObject : ISearchObject<TKey>
    {
        if (typeof(TSearchObject).IsAbstract || (!typeof(TSearchObject).IsValueType && typeof(TSearchObject).GetConstructor(Type.EmptyTypes) == null))
        {
            return true;
        }

        var filters = provider.GetServices<IFilteredQueryBuilder<TEntity, TKey, TSearchObject>>().ToArray();
        if (filters.Any(f => IsRegiras(f.GetType())))
        {
            return true;
        }

        var source = Array.Empty<TEntity>().AsQueryable();
        string Describe(string? q)
        {
            var so = Activator.CreateInstance<TSearchObject>();
            so.Q = q;
            var query = filters.Aggregate(source, (current, filter) => filter.Build(current, so));
            return CapturedValueWriter.Describe(query.Expression);
        }

        return Describe(null) != Describe(QProbe);
    }

    /// <summary>A filter one of Regira's packages declares; a <c>Filter(...)</c> lambda is the app's own code in Regira's wrapper.</summary>
    private static bool IsRegiras(Type filterType)
    {
        var definition = filterType.IsGenericType ? filterType.GetGenericTypeDefinition() : filterType;
        return definition != typeof(EntityQueryFilter<,,>)
            && definition.Assembly.GetName().Name?.StartsWith("Regira.", StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Writes a query out with the values it captured: a field of a captured object (a closure, the filter itself) and a
    /// property of the search object become their value, and a captured collection lists its items. Without that, a
    /// query that captured <c>so</c>, or a value taken from it, reads the same whatever <c>Q</c> holds. Only fields and
    /// the search object's own properties are read, so nothing with side effects runs.
    /// </summary>
    private sealed class CapturedValueWriter : ExpressionVisitor
    {
        private readonly StringBuilder _collections = new();

        public static string Describe(Expression expression)
        {
            var writer = new CapturedValueWriter();
            var written = writer.Visit(expression);
            return written + writer._collections.ToString();
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var target = Visit(node.Expression);
            if (target is ConstantExpression { Value: { } instance })
            {
                switch (node.Member)
                {
                    case FieldInfo field:
                        return Visit(Expression.Constant(field.GetValue(instance), node.Type));
                    case PropertyInfo property when instance is ISearchObject:
                        return Visit(Expression.Constant(property.GetValue(instance), node.Type));
                }
            }
            return node.Update(target);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is IEnumerable items and not string and not IQueryable)
            {
                _collections.Append(" [").AppendJoin(", ", items.Cast<object?>().Take(100)).Append(']');
            }
            return node;
        }
    }
}
