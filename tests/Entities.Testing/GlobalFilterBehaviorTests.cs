using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;

namespace Entities.Testing;

[TestFixture]
public class GlobalFilterBehaviorTests
{
    private class GuidArchivableItem : IEntity<Guid>, IArchivable
    {
        public Guid Id { get; set; }
        public bool IsArchived { get; set; }
    }
    private record GuidItemSearchObject : SearchObject<Guid>;

    private static IQueryable<GuidArchivableItem> CreateItems() => new[]
    {
        new GuidArchivableItem { Id = Guid.NewGuid(), IsArchived = false },
        new GuidArchivableItem { Id = Guid.NewGuid(), IsArchived = true }
    }.AsQueryable();

    // FilterArchivable translation. On net10 hiding archived rows is the named EF query filter's job
    // (ModelBuilderExtensions.SetArchivedQueryFilter, exercised end-to-end in ArchivedRoundTripTests), so
    // Excluded composes nothing and only the opt-ins show up here. On net8 no query filter is installed —
    // suspending one would also suspend the app's own — so Excluded composes the predicate itself, which
    // makes it work on a plain (non-EF) IQueryable like the one below.
    [Test]
    public void FilterArchivable_Excluded_Composition()
    {
        var items = CreateItems().FilterArchivable(ArchivedFilter.Excluded).ToArray();
#if NET10_0_OR_GREATER
        // the EF filter already hides them; composing a second predicate would be redundant
        Assert.That(items, Has.Length.EqualTo(2));
#else
        Assert.That(items, Has.Length.EqualTo(1));
        Assert.That(items[0].IsArchived, Is.False);
#endif
    }

    [Test]
    public void FilterArchivable_Included_Returns_Everything()
    {
        var items = CreateItems().FilterArchivable(ArchivedFilter.Included).ToArray();
        Assert.That(items, Has.Length.EqualTo(2));
    }

    [Test]
    public void FilterArchivable_Only_Narrows_To_Archived()
    {
        var items = CreateItems().FilterArchivable(ArchivedFilter.Only).ToArray();
        Assert.That(items, Has.Length.EqualTo(1));
        Assert.That(items[0].IsArchived, Is.True);
    }

    [Test]
    public void FilterArchivablesQueryBuilder_Uses_The_Configured_Default()
    {
        var builder = new FilterArchivablesQueryBuilder<Guid>(new EntityQueryOptions { DefaultArchivedFilter = ArchivedFilter.Only });
        var items = ((IGlobalFilteredQueryBuilder<IArchivable, Guid>)builder)
            .Build(CreateItems(), new GuidItemSearchObject())
            .ToArray();
        Assert.That(items, Has.Length.EqualTo(1), "a search object without an opinion falls back to the configured default");
        Assert.That(items[0].IsArchived, Is.True);
    }

    [Test]
    public void An_Explicit_Archived_Wins_Over_The_Configured_Default()
    {
        var builder = new FilterArchivablesQueryBuilder<Guid>(new EntityQueryOptions { DefaultArchivedFilter = ArchivedFilter.Only });
        var items = ((IGlobalFilteredQueryBuilder<IArchivable, Guid>)builder)
            .Build(CreateItems(), new GuidItemSearchObject { Archived = ArchivedFilter.Included })
            .ToArray();
        Assert.That(items, Has.Length.EqualTo(2));
    }

    // Global-filter dispatch: one variant per family, key-matching preferred, safe default otherwise.
    [Test]
    public void Only_Int_Filter_Does_Not_Apply_A_Guid_SearchObjects_Archived()
    {
        // Reproduces the UseDefaults() case: only the int-keyed archive filter is registered while the
        // entity is Guid-keyed. The int variant cannot bind the Guid search object, so it null-coerces it
        // and applies its key-agnostic default (Excluded) instead of stepping aside. Archived rows stay
        // hidden either way — by the EF query filter on net10, by that default's own predicate on net8
        // (SoftDeleteLeakRegressionTests pins it end-to-end on both).
        var queryBuilder = new QueryBuilder<GuidArchivableItem, Guid>([new FilterArchivablesQueryBuilder()]);

        var items = queryBuilder.Filter(CreateItems(), new GuidItemSearchObject { Archived = ArchivedFilter.Only }).ToArray();

#if NET10_0_OR_GREATER
        Assert.That(items, Has.Length.EqualTo(2), "the int variant must not read a Guid search object");
#else
        Assert.That(items, Has.Length.EqualTo(1), "the int variant must not read a Guid search object");
        Assert.That(items[0].IsArchived, Is.False, "it applies its key-agnostic Excluded default instead");
#endif
    }

    [Test]
    public void Both_Variants_Registered_Guid_Variant_Honors_Archived()
    {
        // int + Guid variants registered: the family is deduplicated to the key-matching Guid variant, so
        // Archived=Only returns archived rows (rather than an empty intersection from double-application).
        var queryBuilder = new QueryBuilder<GuidArchivableItem, Guid>(
            [new FilterArchivablesQueryBuilder(), new FilterArchivablesQueryBuilder<Guid>()]);

        var items = queryBuilder.Filter(CreateItems(), new GuidItemSearchObject { Archived = ArchivedFilter.Only }).ToArray();

        Assert.That(items, Has.Length.EqualTo(1));
        Assert.That(items[0].IsArchived, Is.True);
    }

    [Test]
    public void Guid_Variant_Honors_Archived_Only()
    {
        var queryBuilder = new QueryBuilder<GuidArchivableItem, Guid>([new FilterArchivablesQueryBuilder<Guid>()]);

        var items = queryBuilder.Filter(CreateItems(), new GuidItemSearchObject { Archived = ArchivedFilter.Only }).ToArray();

        Assert.That(items, Has.Length.EqualTo(1));
        Assert.That(items[0].IsArchived, Is.True);
    }

    // Custom filters derived straight from the abstract base (the documented pattern) are each their own
    // family — the abstract base must never act as a family identity, or distinct filters dedupe away.
    private interface IFlagged
    {
        bool HideByFirst { get; }
        bool HideBySecond { get; }
    }
    private class FlaggedItem : IEntity<int>, IArchivable, IFlagged
    {
        public int Id { get; set; }
        public bool IsArchived { get; set; }
        public bool HideByFirst { get; set; }
        public bool HideBySecond { get; set; }
    }
    private record FlaggedSearchObject : SearchObject;

    private class FirstCustomFilter : GlobalFilteredQueryBuilderBase<IFlagged, int>
    {
        public override IQueryable<IFlagged> Build(IQueryable<IFlagged> query, ISearchObject<int>? so)
            => query.Where(x => !x.HideByFirst);
    }
    private class SecondCustomFilter : GlobalFilteredQueryBuilderBase<IFlagged, int>
    {
        public override IQueryable<IFlagged> Build(IQueryable<IFlagged> query, ISearchObject<int>? so)
            => query.Where(x => !x.HideBySecond);
    }

    private static IQueryable<FlaggedItem> CreateFlaggedItems() => new[]
    {
        new FlaggedItem { Id = 1 },
        new FlaggedItem { Id = 2, HideByFirst = true },
        new FlaggedItem { Id = 3, HideBySecond = true },
        new FlaggedItem { Id = 4, IsArchived = true },
    }.AsQueryable();

    [Test]
    public void Two_Custom_Filters_Derived_From_The_Abstract_Base_Both_Run()
    {
        var queryBuilder = new QueryBuilder<FlaggedItem, int>([new FirstCustomFilter(), new SecondCustomFilter()]);

        var items = queryBuilder.Filter(CreateFlaggedItems(), new FlaggedSearchObject()).ToArray();

        Assert.That(items.Select(x => x.Id), Is.EquivalentTo(new[] { 1, 4 }),
            "both custom filters must run — the abstract base is not a family identity");
    }

    [Test]
    public void Custom_Filter_Coexists_With_A_BuiltIn_Filter()
    {
        var queryBuilder = new QueryBuilder<FlaggedItem, int>([new FilterArchivablesQueryBuilder(), new FirstCustomFilter()]);

        // Archived=Only is the archive filter's observable composition (Excluded delegates to the EF filter)
        var items = queryBuilder.Filter(CreateFlaggedItems(), new FlaggedSearchObject { Archived = ArchivedFilter.Only }).ToArray();

        Assert.That(items.Select(x => x.Id), Is.EquivalentTo(new[] { 4 }),
            "the built-in archive filter and the custom filter must both run");
    }

    // A generic filter class instantiated for two different entity scopes (AccessFilter<IHasOwner> +
    // AccessFilter<IHasDepartment>) shares one generic type definition — but they filter DIFFERENT rows
    // and both must run, or a row-scoping/security filter is silently dropped.
    private interface IHasFirstScope { bool BlockedByFirst { get; } }
    private interface IHasSecondScope { bool BlockedBySecond { get; } }
    private class ScopedItem : IEntity<int>, IHasFirstScope, IHasSecondScope
    {
        public int Id { get; set; }
        public bool BlockedByFirst { get; set; }
        public bool BlockedBySecond { get; set; }
    }

    private class ScopeFilter<TScope> : GlobalFilteredQueryBuilderBase<TScope, int>
        where TScope : class
    {
        private readonly Func<TScope, bool> _blocked;
        public ScopeFilter(Func<TScope, bool> blocked) => _blocked = blocked;
        public override IQueryable<TScope> Build(IQueryable<TScope> query, ISearchObject<int>? so)
            => query.Where(x => !_blocked(x));
    }

    [Test]
    public void Two_Closed_Instantiations_Of_One_Generic_Filter_Both_Run()
    {
        var items = new[]
        {
            new ScopedItem { Id = 1 },
            new ScopedItem { Id = 2, BlockedByFirst = true },
            new ScopedItem { Id = 3, BlockedBySecond = true },
        }.AsQueryable();

        var queryBuilder = new QueryBuilder<ScopedItem, int>([
            new ScopeFilter<IHasFirstScope>(x => x.BlockedByFirst),
            new ScopeFilter<IHasSecondScope>(x => x.BlockedBySecond),
        ]);

        var result = queryBuilder.Filter(items, new SearchObject()).ToArray();

        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { 1 }),
            "both scoped filters must run — a shared generic type definition targeting different entity scopes is not one family");
    }

    // A global filter may scope the CONCRETE entity type, not just an interface it implements. The
    // applicability test must match it, or a registered security filter is silently inert.
    private class ConcreteScopedFilter : GlobalFilteredQueryBuilderBase<ScopedItem, int>
    {
        public override IQueryable<ScopedItem> Build(IQueryable<ScopedItem> query, ISearchObject<int>? so)
            => query.Where(x => !x.BlockedByFirst);
    }

    private static IQueryable<ScopedItem> CreateScopedItems() => new[]
    {
        new ScopedItem { Id = 1 },
        new ScopedItem { Id = 2, BlockedByFirst = true },
        new ScopedItem { Id = 3, BlockedBySecond = true },
    }.AsQueryable();

    [Test]
    public void Filter_Scoped_To_The_Concrete_Entity_Type_Runs()
    {
        var queryBuilder = new QueryBuilder<ScopedItem, int>([new ConcreteScopedFilter()]);

        var result = queryBuilder.Filter(CreateScopedItems(), new SearchObject()).ToArray();

        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { 1, 3 }),
            "a global filter scoped to the concrete entity type must be applied");
    }

    [Test]
    public void Concrete_Scoped_Filter_Runs_Alongside_An_Interface_Scoped_Filter()
    {
        // An interface-wide filter plus a narrower one over the concrete entity: both must run, and the
        // concrete one must not be dropped as inapplicable.
        var queryBuilder = new QueryBuilder<ScopedItem, int>([
            new ScopeFilter<IHasSecondScope>(x => x.BlockedBySecond),
            new ConcreteScopedFilter(),
        ]);

        var result = queryBuilder.Filter(CreateScopedItems(), new SearchObject()).ToArray();

        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { 1 }),
            "an interface-scoped and a concrete-scoped filter must compose, not shadow each other");
    }
}
