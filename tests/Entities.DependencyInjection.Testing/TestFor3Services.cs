using Entities.DependencyInjection.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestFor3Services
{
    [Test]
    public void With_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.UseDefaults())
            .For<Course, int, CourseSearchObject>()
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(entityNormalizer, Is.TypeOf<DefaultEntityNormalizer>());
        Assert.That(globalFilters, Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterIdsQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasCreatedQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasLastModifiedQueryBuilder<int>>(), Is.Not.Empty);

        Assert.That(primers, Is.Not.Empty);
        Assert.That(primers.OfType<HasCreatedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<HasLastModifiedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<ArchivablePrimer>(), Is.Not.Empty);

        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void Without_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>()
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_GlobalFilter()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.AddGlobalFilterQueryBuilder<FilterArchivablesQueryBuilder>())
            .For<Course, int, CourseSearchObject>()
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(globalFilters, Is.Not.Empty);
        Assert.That(globalFilters.Length, Is.EqualTo(1));
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder>(), Is.Not.Empty);

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_Sort()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.SortBy(query => query.OrderBy(x => x.Title));
            })
            .BuildServiceProvider();
        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(sortableBuilder, Is.Not.Null);
        Assert.That(sortableBuilder, Is.TypeOf<SortedQueryBuilder<Course, int>>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_Include()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.Includes((query, _) => query.Include(x => x.Instructors));
            })
            .BuildServiceProvider();
        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(includableBuilder, Is.Not.Null);
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<Course, int>>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_FilterQueryBuilder()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.AddFilter<CourseQueryFilter1>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(queryFilters.First(), Is.TypeOf<CourseQueryFilter1>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_QueryFilter()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.Filter((query, so) =>
                {
                    if (!string.IsNullOrWhiteSpace(so?.Q))
                    {
                        query = query.Where(x => x.Title!.Contains(so.Q));
                    }
                    return query;
                });
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(queryFilters.First(), Is.TypeOf<EntityQueryFilter<Course, int, CourseSearchObject>>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_Processor()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e => e.Process((item, _) => item.HasAttachment = item.Attachments?.Any()))
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(entityProcessors.Length, Is.EqualTo(1));
        Assert.That(entityProcessors.OfType<EntityProcessor<Course, EntityIncludes>>(), Is.Not.Empty);

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_Primer()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e => e.AddPrimer<CoursePrimer>())
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(primers, Is.Not.Empty);
        Assert.That(primers.First(), Is.TypeOf<CoursePrimer>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e => e.HasRepository<CourseRepository3B>())
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(repo2, Is.TypeOf<CourseRepository3B>());
        Assert.That(repo3, Is.TypeOf<CourseRepository3B>());
        Assert.That(entityService2, Is.TypeOf<CourseRepository3B>());
        Assert.That(entityService3, Is.TypeOf<CourseRepository3B>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
    }

    [Test]
    public void With_Manager()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.HasManager<CourseManager3B>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var manager2 = sp.GetService<IEntityManager<Course, int>>();
        var manager3 = sp.GetService<IEntityManager<Course, int, CourseSearchObject>>();

        Assert.That(manager2, Is.TypeOf<CourseManager3B>());
        Assert.That(manager3, Is.TypeOf<CourseManager3B>());
        Assert.That(entityService2, Is.TypeOf<CourseManager3B>());
        Assert.That(entityService3, Is.TypeOf<CourseManager3B>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_EntityService()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e => e.UseEntityService<CourseService3B>())
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(entityService2, Is.TypeOf<CourseService3B>());
        Assert.That(entityService3, Is.TypeOf<CourseService3B>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void With_Custom_EntityService()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e =>
            {
                // define custom EntityService interface
                e.AddTransient<ICourseService3B, CourseRepository3B>();
                e.UseEntityService<CustomCourseService3B>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();

        Assert.That(entityService2, Is.TypeOf<CustomCourseService3B>());
        Assert.That(entityService3, Is.TypeOf<CustomCourseService3B>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void Full_Option()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e =>
            {
                e.UseDefaults();
                e.AddGlobalFilterQueryBuilder<HasAttachmentGlobalQueryFilter>();
            })
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.SortBy(query => query.OrderBy(x => x.Title));
                e.Includes((query, _) => query.Include(x => x.Instructors));
                e.AddFilter<CourseQueryFilter1>();
                e.AddPrimer<CoursePrimer>();
                e.HasRepository<CourseRepository3B>();
                e.HasManager<CourseManager3B>();
                e.AddTransient<ICourseService3B, CourseManager3B>();
                e.UseEntityService<CustomCourseService3B>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var manager2 = sp.GetService<IEntityManager<Course, int>>();
        var manager3 = sp.GetService<IEntityManager<Course, int, CourseSearchObject>>();

        Assert.That(entityNormalizer, Is.TypeOf<DefaultEntityNormalizer>());
        Assert.That(globalFilters.Length, Is.EqualTo(6));
        Assert.That(globalFilters.OfType<FilterIdsQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasCreatedQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasLastModifiedQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasNormalizedContentQueryBuilder>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<HasAttachmentGlobalQueryFilter>(), Is.Not.Empty);
        Assert.That(queryFilters.First(), Is.TypeOf<CourseQueryFilter1>());
        Assert.That(sortableBuilder, Is.TypeOf<SortedQueryBuilder<Course, int>>());
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<Course, int>>());
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, int, CourseSearchObject>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, int, CourseSearchObject>>());
        Assert.That(primers.Length, Is.EqualTo(4));
        Assert.That(primers.OfType<HasCreatedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<HasLastModifiedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<ArchivablePrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<CoursePrimer>(), Is.Not.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course, int>>());
        Assert.That(repo2, Is.TypeOf<CourseRepository3B>());
        Assert.That(repo3, Is.TypeOf<CourseRepository3B>());
        Assert.That(entityService2, Is.TypeOf<CustomCourseService3B>());
        Assert.That(entityService3, Is.TypeOf<CustomCourseService3B>());
        Assert.That(manager2, Is.TypeOf<CourseManager3B>());
        Assert.That(manager3, Is.TypeOf<CourseManager3B>());
    }

    // Regression: the single-type-arg Related<TRelated>() shortcut must compile and wire a
    // RelatedCollectionPrepper on the simple 3-arg builder (previously only on int builders).
    [Test]
    public void Related_Shortcut_Registers_Prepper()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, int, CourseSearchObject>(e => e.Related(x => x.Enrollments))
            .BuildServiceProvider();

        var preppers = sp.GetServices<IEntityPrepper>().ToArray();
        Assert.That(preppers.OfType<RelatedCollectionPrepper<ContosoContext, Course, Enrollment, int, int>>(), Is.Not.Empty);
    }
}