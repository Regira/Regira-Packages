using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Keywords;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;
using System.Linq.Expressions;
using System.Reflection;

namespace Regira.Entities.EFcore.Extensions;

public static class QueryExtensions
{
    public static IQueryable<TEntity> FilterId<TEntity, TKey>(this IQueryable<TEntity> query, TKey? id)
        where TEntity : IEntity<TKey>
    {
        if (!id?.Equals(default(TKey)) ?? false)
        {
            query = query.Where(x => x.Id!.Equals(id));
        }

        return query;
    }
    public static IQueryable<TEntity> FilterIds<TEntity, TKey>(this IQueryable<TEntity> query, ICollection<TKey>? ids)
        where TEntity : IEntity<TKey>
    {
        if (ids?.Any() == true)
        {
            query = query.Where(x => ids.Contains(x.Id));
        }

        return query;
    }
    public static IQueryable<TEntity> FilterExclude<TEntity, TKey>(this IQueryable<TEntity> query, ICollection<TKey>? ids)
        where TEntity : IEntity<TKey>
    {
        if (ids?.Any() == true)
        {
            query = query.Where(x => !ids.Contains(x.Id));
        }

        return query;
    }

    public static IQueryable<TEntity> FilterCode<TEntity>(this IQueryable<TEntity> query, string? code)
        where TEntity : IHasCode
    {
        if (code != null)
        {
            query = query.Where(x => x.Code == code);
        }

        return query;
    }

    public static IQueryable<TEntity> FilterTitle<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords)
        where TEntity : IHasTitle
    {
        if (keywords?.Any() == true)
        {
            foreach (var q in keywords)
            {
                query = q.HasWildcard
                    ? query.Where(x => EF.Functions.Like(x.Title!, q.Q!))
                    : query.Where(x => x.Title == q.Keyword);
            }
        }

        return query;
    }
    public static IQueryable<TEntity> FilterNormalizedTitle<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords)
        where TEntity : IHasNormalizedTitle
    {
        if (keywords?.Any() == true)
        {
            foreach (var q in keywords)
            {
                query = q.HasWildcard
                    ? query.Where(x => EF.Functions.Like(x.NormalizedTitle!, q.Q!))
                    : query.Where(x =>
                        x.NormalizedTitle == q.Normalized || x.NormalizedTitle!.Contains($" {q.Normalized}") ||
                        x.NormalizedTitle!.Contains($"{q.Normalized} "));
            }
        }

        return query;
    }

    /// <summary>
    /// Filters on <see cref="IHasCreated.Created"/>. With <see cref="DateTimeDefaults.UseUtc"/> enabled (default),
    /// input dates are normalized to UTC (local kinds converted, unspecified kinds assumed UTC) to match the stored UTC timestamps.
    /// </summary>
    public static IQueryable<TEntity> FilterCreated<TEntity>(this IQueryable<TEntity> query, DateTime? minDate, DateTime? maxDate)
        where TEntity : IHasCreated
    {
        if (minDate.HasValue)
        {
            var min = NormalizeDate(minDate.Value);
            query = query.Where(x => x.Created >= min);
        }
        if (maxDate.HasValue)
        {
            var max = NormalizeDate(maxDate.Value);
            query = query.Where(x => x.Created <= max);
        }

        return query;
    }
    /// <summary>
    /// Filters on <see cref="IHasLastModified.LastModified"/>. With <see cref="DateTimeDefaults.UseUtc"/> enabled (default),
    /// input dates are normalized to UTC (local kinds converted, unspecified kinds assumed UTC) to match the stored UTC timestamps.
    /// </summary>
    public static IQueryable<TEntity> FilterLastModified<TEntity>(this IQueryable<TEntity> query, DateTime? minDate, DateTime? maxDate)
        where TEntity : IHasLastModified
    {
        if (minDate.HasValue)
        {
            var min = NormalizeDate(minDate.Value);
            query = query.Where(x => x.LastModified >= min);
        }
        if (maxDate.HasValue)
        {
            var max = NormalizeDate(maxDate.Value);
            query = query.Where(x => x.LastModified <= max);
        }

        return query;
    }
    public static IQueryable<TEntity> FilterTimestamps<TEntity>(this IQueryable<TEntity> query, DateTime? minCreated, DateTime? maxCreated, DateTime? minModified, DateTime? maxModified)
        where TEntity : IHasTimestamps
        => query
            .FilterCreated(minCreated, maxCreated)
            .FilterLastModified(minModified, maxModified);

    /// <summary>
    /// Filters on <see cref="IHasStartEndDate"/>. With <see cref="DateTimeDefaults.UseUtc"/> enabled (default),
    /// the input date is normalized to UTC, matching the stored values.<br />
    /// For pure calendar semantics (no time component) prefer <see cref="DateOnly"/> properties over <see cref="IHasStartEndDate"/>.
    /// </summary>
    public static IQueryable<TEntity> FilterIsActiveOn<TEntity>(this IQueryable<TEntity> query, DateTime? date)
        where TEntity : IHasStartEndDate
    {
        DateTime? value = date.HasValue ? NormalizeDate(date.Value) : null;
        return query.Where(x => (x.StartDate == null || x.StartDate <= value) && (x.EndDate == null || x.EndDate >= value));
    }

    public static IQueryable<TEntity> FilterQ<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords)
        where TEntity : IHasNormalizedContent
    {
        if (keywords?.Any() == true)
        {
            foreach (var q in keywords)
            {
                query = query.Where(x => EF.Functions.Like(x.NormalizedContent!, q.QW!));
            }
        }

        return query;
    }

    /// <summary>
    /// Keyword search over explicit fields, for entities that do not implement <see cref="IHasNormalizedContent"/>.
    /// Each keyword must match at least one <paramref name="field"/> (OR across fields, AND across keywords), so
    /// "acme 2024" matches a row whose code carries one term and whose related customer carries the other.
    /// <para>
    /// Use inside an <c>IFilteredQueryBuilder</c> (or a <c>Filter(...)</c> callback) to make <c>?q=</c> functional —
    /// without one of those, <c>q</c> is silently ignored for such entities. Matching uses
    /// <see cref="QKeyword.TrimmedQW"/> (the <b>raw</b> keyword, wildcards at both ends), because the fields
    /// named here are ordinary columns storing the client's value verbatim — a code, a name. For a single
    /// searchable blob, implement <see cref="IHasNormalizedContent"/> and use the
    /// <c>ParsedKeywordCollection</c> overload instead, which matches <see cref="QKeyword.QW"/> against
    /// <c>NormalizedContent</c>.
    /// </para>
    /// <para>
    /// If a field you name here is itself a <em>normalized</em> column (<c>NormalizedLastName</c> and the
    /// like), this will not match it once the term carries punctuation — build that predicate with
    /// <see cref="QKeyword.QW"/> yourself rather than passing the column to this overload.
    /// </para>
    /// <para>
    /// The keywords come from <c>IQKeywordHelper</c>, which is a registered service — hence the builder class
    /// below rather than an inline <c>e.Filter((query, so) =&gt; …)</c> lambda, which receives no injected
    /// services. <c>new QKeywordHelper()</c> does compile inside a lambda, but it bypasses the app's configured
    /// <c>QKeywordHelperOptions</c> and normalizer, so the parsed keywords stop matching what was stored.
    /// </para>
    /// </summary>
    /// <example><code>
    /// public class OrderQueryBuilder(IQKeywordHelper qHelper) : FilteredQueryBuilderBase&lt;Order, int, OrderSearchObject&gt;
    /// {
    ///     public override IQueryable&lt;Order&gt; Build(IQueryable&lt;Order&gt; query, OrderSearchObject? so)
    ///         =&gt; query.FilterQ(qHelper.Parse(so?.Q), x =&gt; x.Code, x =&gt; x.Customer!.Name);
    /// }
    /// // e.AddFilter&lt;OrderQueryBuilder&gt;();
    /// </code></example>
    public static IQueryable<TEntity> FilterQ<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords,
        Expression<Func<TEntity, string?>> field, params Expression<Func<TEntity, string?>>[] moreFields)
    {
        if (keywords?.Any() != true)
        {
            return query;
        }

        // one field is required positionally: a params-only signature would bind a field-less call to an
        // empty array and silently filter nothing
        Expression<Func<TEntity, string?>>[] fields = [field, .. moreFields];
        foreach (var keyword in keywords)
        {
            query = query.Where(AnyFieldLike(fields, keyword.TrimmedQW!));
        }

        return query;
    }

    /// <summary>
    /// Translates an <see cref="ArchivedFilter"/> onto the query. Which mechanism hides archived rows — and
    /// therefore what the two opt-ins have to do — depends on the target framework:
    /// <list type="bullet">
    /// <item><b><c>net10.0</c> (EF Core 10)</b>: the <em>named</em> query filter installed by
    /// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> hides them — that is also what reaches
    /// into <c>Include(...)</c> — so <see cref="ArchivedFilter.Excluded"/> composes nothing and the opt-ins
    /// suspend that one filter by name (<see cref="ModelBuilderExtensions.ArchivedQueryFilterName"/>).
    /// Query filters the app configured itself are named too and keep applying.</item>
    /// <item><b><c>net8.0</c> (EF Core 9)</b>: no query filter is installed, and none is ever suspended —
    /// this composes the predicate at the root of the query instead. EF Core 9 has no named filters, so an
    /// opt-in could only be expressed as the untargeted <c>IgnoreQueryFilters()</c>, which would also drop
    /// the app's own query filters; since the write path resolves its original archived-inclusive on
    /// <em>every</em> update, row security expressed as a <c>HasQueryFilter</c> would become a cross-tenant
    /// read <b>and write</b>. The trade-off is that archived rows are not filtered out of an
    /// <c>Include(...)</c>d collection there.</item>
    /// </list>
    /// Every <c>IGlobalFilteredQueryBuilder</c> (tenant/owner row security) is a plain <c>Where</c> in the
    /// query pipeline, not an EF query filter, so none of this can widen it on either target.
    /// </summary>
    public static IQueryable<TEntity> FilterArchivable<TEntity>(this IQueryable<TEntity> query, ArchivedFilter archived)
        where TEntity : class, IArchivable
        => archived switch
        {
#if NET10_0_OR_GREATER
            ArchivedFilter.Included => query.IgnoreQueryFilters([ModelBuilderExtensions.ArchivedQueryFilterName]),
            ArchivedFilter.Only => query.IgnoreQueryFilters([ModelBuilderExtensions.ArchivedQueryFilterName]).Where(x => x.IsArchived),
            _ => query
#else
            ArchivedFilter.Included => query,
            ArchivedFilter.Only => query.Where(x => x.IsArchived),
            _ => query.Where(x => !x.IsArchived)
#endif
        };

    public static IQueryable<TEntity> FilterHasAttachment<TEntity>(this IQueryable<TEntity> query, bool? hasAttachment)
        where TEntity : IHasAttachments
    {
        if (hasAttachment.HasValue)
        {
            query = query.Where(x => hasAttachment == x.Attachments!.Any());
        }

        return query;
    }

    public static IQueryable<TEntity> SortQuery<TEntity, TKey>(this IQueryable<TEntity> query)
        where TEntity : IEntity<TKey>
    {
        if (TypeUtility.ImplementsInterface<IHasNormalizedTitle>(typeof(TEntity)))
        {
            return query.OrderOrThenBy(x => ((IHasNormalizedTitle)x).NormalizedTitle);
        }
        if (TypeUtility.ImplementsInterface<IHasTitle>(typeof(TEntity)))
        {
            return query.OrderOrThenBy(x => ((IHasTitle)x).Title);
        }

        return query.OrderOrThenBy(x => x.Id);
    }

    /// <summary>
    /// Continues an existing ordering with <c>ThenBy</c>, or starts one with <c>OrderBy</c> when the
    /// query is not ordered yet. Use this inside <c>SortBy</c> builders: EF Core query objects satisfy
    /// <c>is IOrderedQueryable&lt;T&gt;</c> even before any ordering is applied, so calling <c>ThenBy</c>
    /// after that check throws <see cref="ArgumentException"/> ("Expression of type 'IQueryable`1' cannot
    /// be used for parameter of type 'IOrderedQueryable`1'") on the first sorted request. This inspects
    /// the expression tree instead.
    /// </summary>
    public static IOrderedQueryable<TEntity> OrderOrThenBy<TEntity, TKey>(this IQueryable<TEntity> query, Expression<Func<TEntity, TKey>> keySelector)
        => IsOrdered(query) && query is IOrderedQueryable<TEntity> sortedQuery
            ? sortedQuery.ThenBy(keySelector)
            : query.OrderBy(keySelector);

    /// <inheritdoc cref="OrderOrThenBy{TEntity,TKey}" />
    public static IOrderedQueryable<TEntity> OrderOrThenByDescending<TEntity, TKey>(this IQueryable<TEntity> query, Expression<Func<TEntity, TKey>> keySelector)
        => IsOrdered(query) && query is IOrderedQueryable<TEntity> sortedQuery
            ? sortedQuery.ThenByDescending(keySelector)
            : query.OrderByDescending(keySelector);

    // The runtime type is not reliable here (see OrderOrThenBy docs) — only the expression tree tells
    // whether an OrderBy has actually been composed yet.
    private static bool IsOrdered(IQueryable query)
        => typeof(IOrderedQueryable).IsAssignableFrom(query.Expression.Type);

    // Date filter inputs follow the ambient UTC policy: normalized to UTC by default, used as given when disabled
    private static DateTime NormalizeDate(DateTime date)
        => DateTimeDefaults.UseUtc ? date.AsUtc() : date;

    private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions)
        .GetMethod(nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;
    private static readonly MemberExpression EfFunctions =
        Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

    /// <summary>
    /// <c>x =&gt; EF.Functions.Like(f1(x), pattern) || EF.Functions.Like(f2(x), pattern) || ...</c>, built as one
    /// lambda so the OR translates to a single SQL predicate. Each selector carries its own parameter instance,
    /// so their bodies are rebound onto a shared one before being combined.
    /// </summary>
    private static Expression<Func<TEntity, bool>> AnyFieldLike<TEntity>(IReadOnlyList<Expression<Func<TEntity, string?>>> fields, string pattern)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var patternValue = Expression.Constant(pattern, typeof(string));
        Expression? predicate = null;

        foreach (var field in fields)
        {
            var body = new ParameterRebinder(field.Parameters[0], parameter).Visit(field.Body)!;
            var like = Expression.Call(LikeMethod, EfFunctions, body, patternValue);
            predicate = predicate == null ? like : Expression.OrElse(predicate, like);
        }

        return Expression.Lambda<Func<TEntity, bool>>(predicate!, parameter);
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}