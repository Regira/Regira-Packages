using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Normalizers;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestWithoutDbContext
{
    [Test]
    public void Without_Defaults()
    {
        var services = new ServiceCollection();
        services.UseEntities();
        using var sp = services.BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(primers, Is.Empty);
    }

    [Test]
    public void With_Defaults()
    {
        var services = new ServiceCollection();
        services.UseEntities(e => e.UseDefaults());
        using var sp = services.BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();

        Assert.That(entityNormalizer, Is.TypeOf<DefaultEntityNormalizer>());
        Assert.That(globalFilters, Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterIdsQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasCreatedQueryBuilder>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasLastModifiedQueryBuilder>(), Is.Not.Empty);
        Assert.That(primers, Is.Not.Empty);
        Assert.That(primers.OfType<HasCreatedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<HasLastModifiedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<ArchivablePrimer>(), Is.Not.Empty);
    }

    [Test]
    public void With_GlobalFilter()
    {
        var services = new ServiceCollection();
        services.UseEntities(e => e.AddGlobalFilterQueryBuilder<FilterArchivablesQueryBuilder>());
        using var sp = services.BuildServiceProvider();

        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();

        Assert.That(globalFilters, Is.Not.Empty);
        Assert.That(globalFilters.Length, Is.EqualTo(1));
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder>(), Is.Not.Empty);
    }

    [Test]
    public void With_Primers()
    {
        var services = new ServiceCollection();
        services.UseEntities(e => e.AddDefaultPrimers());
        using var sp = services.BuildServiceProvider();

        var primers = sp.GetServices<IEntityPrimer>().ToArray();

        Assert.That(primers, Is.Not.Empty);
        Assert.That(primers.OfType<HasCreatedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<HasLastModifiedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<ArchivablePrimer>(), Is.Not.Empty);
    }

    [Test]
    public void With_Normalizer()
    {
        var services = new ServiceCollection();
        services.UseEntities(e => e.AddDefaultEntityNormalizer());
        using var sp = services.BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();

        Assert.That(entityNormalizer, Is.TypeOf<DefaultEntityNormalizer>());
    }

    [Test]
    public void Returns_Options_With_Services()
    {
        var services = new ServiceCollection();
        var options = services.UseEntities();

        Assert.That(options, Is.Not.Null);
        Assert.That(options.Services, Is.SameAs(services));
    }

    [Test]
    public void Registers_IServiceCollection_Singleton()
    {
        var services = new ServiceCollection();
        services.UseEntities();
        using var sp = services.BuildServiceProvider();

        var registeredServiceCollection = sp.GetService<IServiceCollection>();

        Assert.That(registeredServiceCollection, Is.SameAs(services));
    }
}
