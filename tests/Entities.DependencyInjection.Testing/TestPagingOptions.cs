using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestPagingOptions
{
    [Test]
    public void Global_PageSize_Is_Registered()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e =>
            {
                e.DefaultPageSize = 10;
                e.MaxPageSize = 100;
            })
            .For<Course>()
            .BuildServiceProvider();

        var globalOptions = sp.GetService<EntityListOptions>();
        var perEntityOptions = sp.GetService<EntityListOptions<Course>>();

        Assert.That(globalOptions, Is.Not.Null);
        Assert.That(globalOptions!.DefaultPageSize, Is.EqualTo(10));
        Assert.That(globalOptions.MaxPageSize, Is.EqualTo(100));

        // no per-entity override registered
        Assert.That(perEntityOptions, Is.Null);
    }

    [Test]
    public void SetPageSize_Registers_PerEntity_Override()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e =>
            {
                e.DefaultPageSize = 10;
                e.MaxPageSize = 100;
            })
            .For<Course>(e => e.SetPageSize(defaultPageSize: 25, maxPageSize: 50))
            .BuildServiceProvider();

        var globalOptions = sp.GetService<EntityListOptions>();
        var perEntityOptions = sp.GetService<EntityListOptions<Course>>();

        // global is untouched
        Assert.That(globalOptions, Is.Not.Null);
        Assert.That(globalOptions!.DefaultPageSize, Is.EqualTo(10));
        Assert.That(globalOptions.MaxPageSize, Is.EqualTo(100));

        // per-entity holds the override
        Assert.That(perEntityOptions, Is.Not.Null);
        Assert.That(perEntityOptions!.DefaultPageSize, Is.EqualTo(25));
        Assert.That(perEntityOptions.MaxPageSize, Is.EqualTo(50));
    }

    [Test]
    public void Global_SetPageSize_NoArgs_After_Defaults_Disables_Paging()
    {
        // reproduces the test API ordering: UseDefaults() sets a forced default, then SetPageSize() opts out
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e =>
            {
                e.DefaultPageSize = 10;   // simulate UseDefaults()
                e.MaxPageSize = 100;
                e.SetPageSize();          // opt out, no args
            })
            .For<Course>()
            .BuildServiceProvider();

        var globalOptions = sp.GetService<EntityListOptions>();

        // SetPageSize() must win over the earlier default at the registration boundary
        Assert.That(globalOptions, Is.Not.Null);
        Assert.That(globalOptions!.DefaultPageSize, Is.Null);
        Assert.That(globalOptions.MaxPageSize, Is.Null);
    }

    [Test]
    public void Multiple_UseEntities_Later_SetPageSize_OptOut_Is_Not_Lost()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContosoContext>();

        // first registration forces a default page size
        services.UseEntities<ContosoContext>(e =>
        {
            e.DefaultPageSize = 10;
            e.MaxPageSize = 100;
        });
        // a later registration opts out -> this must win, not be swallowed by the first
        services.UseEntities(e => e.SetPageSize());

        using var sp = services.BuildServiceProvider();
        var globalOptions = sp.GetService<EntityListOptions>();

        Assert.That(globalOptions, Is.Not.Null);
        Assert.That(globalOptions!.DefaultPageSize, Is.Null);
        Assert.That(globalOptions.MaxPageSize, Is.Null);
    }

    [Test]
    public void Multiple_UseEntities_Without_Paging_Config_Keep_Earlier_Default()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContosoContext>();

        // first registration forces a default page size
        services.UseEntities<ContosoContext>(e =>
        {
            e.DefaultPageSize = 10;
            e.MaxPageSize = 100;
        });
        // a later registration that does not touch paging must not clobber the configured default
        services.UseEntities(_ => { });

        using var sp = services.BuildServiceProvider();
        var globalOptions = sp.GetService<EntityListOptions>();

        Assert.That(globalOptions, Is.Not.Null);
        Assert.That(globalOptions!.DefaultPageSize, Is.EqualTo(10));
        Assert.That(globalOptions.MaxPageSize, Is.EqualTo(100));
    }

    [Test]
    public void SetPageSize_Without_Arguments_Registers_OptOut()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.DefaultPageSize = 10)
            .For<Course>(e => e.SetPageSize())
            .BuildServiceProvider();

        var perEntityOptions = sp.GetService<EntityListOptions<Course>>();

        // present but both null => opt-out (fully overrides the global default)
        Assert.That(perEntityOptions, Is.Not.Null);
        Assert.That(perEntityOptions!.DefaultPageSize, Is.Null);
        Assert.That(perEntityOptions.MaxPageSize, Is.Null);
    }
}
