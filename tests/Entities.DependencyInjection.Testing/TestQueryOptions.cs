using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Models;
using Regira.Entities.QueryBuilders.Abstractions;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestQueryOptions
{
    [Test]
    public void Global_QueryOptions_Are_Registered_With_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .GetServices()
            .BuildServiceProvider();

        var queryOptions = sp.GetService<EntityQueryOptions>();

        Assert.That(queryOptions, Is.Not.Null);
        Assert.That(queryOptions!.DefaultArchivedFilter, Is.EqualTo(ArchivedFilter.Excluded));
    }

    [Test]
    public void Configured_QueryOptions_Flow_Into_The_Singleton()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.DefaultArchivedFilter = ArchivedFilter.Included)
            .GetServices()
            .BuildServiceProvider();

        var queryOptions = sp.GetService<EntityQueryOptions>();

        Assert.That(queryOptions, Is.Not.Null);
        Assert.That(queryOptions!.DefaultArchivedFilter, Is.EqualTo(ArchivedFilter.Included));
    }

    [Test]
    public void Multiple_UseEntities_Without_Query_Config_Keep_Earlier_Configuration()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContosoContext>();

        services.UseEntities<ContosoContext>(e => e.DefaultArchivedFilter = ArchivedFilter.Included);
        // a later registration that does not touch query behavior must not clobber the configured value
        services.UseEntities(_ => { });

        using var sp = services.BuildServiceProvider();
        var queryOptions = sp.GetService<EntityQueryOptions>();

        Assert.That(queryOptions, Is.Not.Null);
        Assert.That(queryOptions!.DefaultArchivedFilter, Is.EqualTo(ArchivedFilter.Included));
    }

    [Test]
    public void AddDefaultGlobalQueryFilters_NonInt_Key_Registers_Keyed_Variants()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>(e => e.AddDefaultGlobalQueryFilters<Guid>());

        var globalFilterImplementations = services
            .Where(d => d.ServiceType == typeof(IGlobalFilteredQueryBuilder))
            .Select(d => d.ImplementationType)
            .ToArray();

        Assert.That(globalFilterImplementations, Does.Contain(typeof(FilterIdsQueryBuilder<Guid>)));
        Assert.That(globalFilterImplementations, Does.Contain(typeof(FilterArchivablesQueryBuilder<Guid>)));
        Assert.That(globalFilterImplementations, Does.Contain(typeof(FilterHasCreatedQueryBuilder<Guid>)));
        Assert.That(globalFilterImplementations, Does.Contain(typeof(FilterHasLastModifiedQueryBuilder<Guid>)));
    }

    [Test]
    public void AddDefaultGlobalQueryFilters_Int_Key_Does_Not_Duplicate_Filters()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>(e => e.AddDefaultGlobalQueryFilters());

        var archivableRegistrations = services
            .Where(d => d.ServiceType == typeof(IGlobalFilteredQueryBuilder))
            .Count(d => d.ImplementationType == typeof(FilterArchivablesQueryBuilder)
                        || d.ImplementationType == typeof(FilterArchivablesQueryBuilder<int>));

        Assert.That(archivableRegistrations, Is.EqualTo(1));
    }
}
