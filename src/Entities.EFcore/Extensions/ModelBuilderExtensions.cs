using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.EFcore.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Name of the EF query filter applied by <see cref="SetArchivedQueryFilter"/>, and the only filter
    /// name the archived opt-out ever ignores. Filters the consuming app defines itself are untouched.
    /// <para>
    /// <c>net10.0</c> only: EF Core 9 has no named query filters, so on <c>net8.0</c> no archived query
    /// filter is installed and this name is never used (see <see cref="SetArchivedQueryFilter"/>).
    /// </para>
    /// </summary>
    public const string ArchivedQueryFilterName = "Regira:Archived";

    /// <summary>
    /// Name given to a query filter the app configured anonymously (<c>HasQueryFilter(x =&gt; ...)</c>) on an
    /// <see cref="IArchivable"/> entity: EF Core 10 refuses to mix an anonymous filter with named ones, so
    /// <see cref="SetArchivedQueryFilter"/> re-registers it under this name. It keeps applying exactly as
    /// before — and, being named, survives the archived opt-out.
    /// <para>
    /// <c>net10.0</c> only, like <see cref="ArchivedQueryFilterName"/>: on <c>net8.0</c> no filter is
    /// re-registered because none is installed.
    /// </para>
    /// </summary>
    public const string ModelQueryFilterName = "Regira:Model";

    /// <summary>
    /// Applies <c>e =&gt; !e.IsArchived</c> as a named EF Core query filter to every entity type implementing
    /// <see cref="IArchivable"/>.
    /// <para>
    /// <b>Calling this is optional.</b> <c>UseEntities&lt;TContext&gt;(o =&gt; o.UseDefaults())</c> installs the
    /// same filter through the context's options (<c>DbContextWiring.ArchivedQueryFilter</c> →
    /// <c>AddArchivedQueryFilter()</c> → <c>ArchivedQueryFilterConvention</c>), so a <c>DbContext</c> registered
    /// with <c>AddDbContext</c> needs no soft-delete plumbing of its own. Call this when the context is built
    /// outside that wiring — a hand-constructed <c>new AppDbContext(options)</c>, or a setup that opted out of
    /// <c>DbContextWiring.ArchivedQueryFilter</c>. Then it belongs in <c>OnModelCreating</c>, <em>after</em> any
    /// query filter the app defines itself, and once:
    /// </para>
    /// <code>
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     base.OnModelCreating(modelBuilder);
    ///     // ... your model configuration ...
    ///     modelBuilder.SetArchivedQueryFilter();
    /// }
    /// </code>
    /// Calling it alongside the automatic wiring is safe: the convention leaves an entity type that already
    /// carries the named filter untouched.
    /// Being a model-level filter it also propagates into <c>Include(...)</c>: archived rows are invisible as
    /// the main entity <em>and</em> inside an included collection. Opting back in
    /// (<see cref="ArchivedFilter.Included"/> / <see cref="ArchivedFilter.Only"/>) is handled by
    /// <c>FilterArchivablesQueryBuilder</c>, which ignores this filter by name — every
    /// <c>IGlobalFilteredQueryBuilder</c> (tenant/owner row security) and every query filter the app defined
    /// itself keeps running regardless. A filter the app configured anonymously on the same entity is
    /// re-registered under <see cref="ModelQueryFilterName"/> (EF Core 10 rejects mixing anonymous and named
    /// filters) and applies exactly as before.
    /// <para>
    /// Unless the filter arrives one way or the other, an <see cref="IArchivable"/> entity is <b>not</b>
    /// filtered at all on <c>net10.0</c>: archived rows become visible everywhere.
    /// <c>ArchivedQueryFilterValidator</c> reports a model that ends up without it at startup.
    /// </para>
    /// <para>
    /// Derived and owned entity types are skipped — EF only accepts a query filter on the root of a
    /// hierarchy, where it already covers the derived types.
    /// </para>
    /// <para>
    /// <b><c>net8.0</c> (EF Core 9): this call is a no-op.</b> EF Core 9 has no named query filters, so the
    /// archived opt-ins could only be honoured with the untargeted <c>IgnoreQueryFilters()</c> — which also
    /// suspends every query filter the app configured itself. Because the write path resolves its original
    /// archived-inclusive on <em>every</em> update, that would turn row security expressed as a
    /// <c>HasQueryFilter</c> into a cross-tenant read <b>and write</b>. There, archived rows are excluded by
    /// <c>FilterArchivablesQueryBuilder</c> at the root of the query instead
    /// (<see cref="QueryExtensions.FilterArchivable{TEntity}"/>): soft delete works without this call, but
    /// archived rows are <em>not</em> filtered out of an <c>Include(...)</c>d collection. Keep the call in
    /// <c>OnModelCreating</c> either way — it is what makes the same model correct on <c>net10.0</c>.
    /// </para>
    /// </summary>
    /// <param name="modelBuilder"></param>
    public static void SetArchivedQueryFilter(this ModelBuilder modelBuilder)
    {
#if !NET10_0_OR_GREATER
        // EF Core 9: deliberately nothing. See the target-framework note above — installing the filter here
        // would force the archived opt-ins onto the untargeted IgnoreQueryFilters(), which suspends the app's
        // own query filters for that query. On the write path (EntityWriteService.Modify resolves the
        // original archived-inclusive, unconditionally) that is a cross-tenant WRITE, not just a read.
#else
        var entityTypes = modelBuilder.Model
            .GetEntityTypes()
            .Where(IsArchivedFilterTarget)
            .ToArray();

        foreach (var entityType in entityTypes)
        {
            var entityBuilder = modelBuilder.Entity(entityType.ClrType);
            // EF Core 10 rejects a model that mixes an anonymous filter with named ones, so a filter the app
            // configured anonymously is re-registered under ModelQueryFilterName. It keeps applying, and
            // stays out of reach of the archived opt-out (which targets ArchivedQueryFilterName only).
            var anonymous = entityType.GetDeclaredQueryFilters().FirstOrDefault(f => f.IsAnonymous)?.Expression;
            if (anonymous != null)
            {
                entityBuilder.HasQueryFilter((LambdaExpression?)null);
                entityBuilder.HasQueryFilter(ModelQueryFilterName, anonymous);
            }
            entityBuilder.HasQueryFilter(ArchivedQueryFilterName, BuildArchivedFilter(entityType.ClrType));
        }
#endif
    }

    /// <summary>
    /// Whether <paramref name="entityType"/> is an entity type the archived query filter belongs on. Shared by
    /// <see cref="SetArchivedQueryFilter"/>, the convention that installs the same filter from the context's
    /// options, and the startup validator that reports its absence — so all three select identically.
    /// <para>
    /// A query filter belongs on the root of a hierarchy (where it already covers the derived types), and an
    /// owned type is configured through its owner.
    /// </para>
    /// </summary>
    internal static bool IsArchivedFilterTarget(IReadOnlyEntityType entityType)
        => typeof(IArchivable).IsAssignableFrom(entityType.ClrType)
           && entityType.BaseType == null
           && !entityType.IsOwned();

    /// <summary><c>e =&gt; !e.IsArchived</c>, typed for <paramref name="clrType"/>.</summary>
    internal static LambdaExpression BuildArchivedFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        Expression body = Expression.Not(Expression.Property(parameter, nameof(IArchivable.IsArchived)));
        return Expression.Lambda(body, parameter);
    }
}
