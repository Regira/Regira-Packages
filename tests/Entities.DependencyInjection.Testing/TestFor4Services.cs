using Entities.DependencyInjection.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestFor4Services
{
    [Test]
    public void With_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.UseDefaults())
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

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
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void Without_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_GlobalFilter()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.AddGlobalFilterQueryBuilder<FilterArchivablesQueryBuilder>())
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(globalFilters, Is.Not.Empty);
        Assert.That(globalFilters.Length, Is.EqualTo(1));
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder>(), Is.Not.Empty);

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Sort()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                e.SortBy(query => query.OrderBy(x => x.Title));
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(sortableBuilder, Is.Not.Null);
        Assert.That(sortableBuilder, Is.TypeOf<SortedQueryBuilder<Course, int>>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Include()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                e.AddIncludes<CourseIncludingQueryBuilder>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(includableBuilder, Is.Not.Null);
        Assert.That(includableBuilder, Is.TypeOf<CourseIncludingQueryBuilder>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Include_Shortcut()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                e.Includes((query, _) => query.Include(x => x.Instructors));
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(includableBuilder, Is.Not.Null);
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<Course, int, CourseIncludes>>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_FilterQueryBuilder()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                e.AddFilter<CourseQueryFilter1>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(queryFilters.First(), Is.TypeOf<CourseQueryFilter1>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_QueryFilter()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
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
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(queryFilters.First(), Is.TypeOf<EntityQueryFilter<Course, int, CourseSearchObject>>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Processor()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e => e.Process((item, _) => item.HasAttachment = item.Attachments?.Any()))
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, CourseIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(entityProcessors.Length, Is.EqualTo(1));
        Assert.That(entityProcessors.OfType<EntityProcessor<Course, CourseIncludes>>(), Is.Not.Empty);

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Primer()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e => e.AddPrimer<CoursePrimer>())
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(primers, Is.Not.Empty);
        Assert.That(primers.First(), Is.TypeOf<CoursePrimer>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityService5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e => e.HasRepository<CourseRepository4>())
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(repo1, Is.TypeOf<CourseRepository4>());
        Assert.That(repo2, Is.TypeOf<CourseRepository4>());
        Assert.That(repo3, Is.TypeOf<CourseRepository4>());
        Assert.That(repo4, Is.TypeOf<CourseRepository4>());
        Assert.That(repo5, Is.TypeOf<CourseRepository4>());
        Assert.That(entityService1, Is.TypeOf<CourseRepository4>());
        Assert.That(entityService2, Is.TypeOf<CourseRepository4>());
        Assert.That(entityService3, Is.TypeOf<CourseRepository4>());
        Assert.That(entityService4, Is.TypeOf<CourseRepository4>());
        Assert.That(entityService5, Is.TypeOf<CourseRepository4>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
    }

    [Test]
    public void With_Manager()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                e.HasManager<CourseManager4>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var manager1 = sp.GetService<IEntityManager<Course>>();
        var manager2 = sp.GetService<IEntityManager<Course, int>>();
        var manager3 = sp.GetService<IEntityManager<Course, int, CourseSearchObject>>();
        var manager4 = sp.GetService<IEntityManager<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var manager5 = sp.GetService<IEntityManager<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(manager1, Is.TypeOf<CourseManager4>());
        Assert.That(manager2, Is.TypeOf<CourseManager4>());
        Assert.That(manager3, Is.TypeOf<CourseManager4>());
        Assert.That(manager4, Is.TypeOf<CourseManager4>());
        Assert.That(manager5, Is.TypeOf<CourseManager4>());
        Assert.That(entityService1, Is.TypeOf<CourseManager4>());
        Assert.That(entityService2, Is.TypeOf<CourseManager4>());
        Assert.That(entityService3, Is.TypeOf<CourseManager4>());
        Assert.That(entityService4, Is.TypeOf<CourseManager4>());
        Assert.That(entityService5, Is.TypeOf<CourseManager4>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_EntityService()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e => e.UseEntityService<CourseService4>())
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(entityService1, Is.TypeOf<CourseService4>());
        Assert.That(entityService2, Is.TypeOf<CourseService4>());
        Assert.That(entityService3, Is.TypeOf<CourseService4>());
        Assert.That(entityService4, Is.TypeOf<CourseService4>());
        Assert.That(entityService5, Is.TypeOf<CourseService4>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
    }

    [Test]
    public void With_Custom_EntityService()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                // define custom EntityService interface
                e.AddTransient<ICourseService5, CourseRepository4>();
                e.UseEntityService<CustomCourseService4>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

        Assert.That(entityService1, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService2, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService3, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService4, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService5, Is.TypeOf<CustomCourseService4>());

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers, Is.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo4, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(repo5, Is.TypeOf<EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
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
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(e =>
            {
                e.SortBy(query => query.OrderBy(x => x.Title));
                e.Includes((query, _) => query.Include(x => x.Instructors));
                e.AddFilter<CourseQueryFilter1>();
                e.AddPrimer<CoursePrimer>();
                e.HasRepository<CourseRepository4>();
                e.HasManager<CourseManager4>();
                e.AddTransient<ICourseService5, CourseManager4>();
                e.UseEntityService<CustomCourseService4>();
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Course, int, CourseSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Course, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Course, int, CourseIncludes>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Course, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Course, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject>>();
        var entityReadService5 = sp.GetService<IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Course, int>>();
        var repo1 = sp.GetService<IEntityRepository<Course>>();
        var repo2 = sp.GetService<IEntityRepository<Course, int>>();
        var repo3 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>();
        var repo4 = sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var repo5 = sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Course>>();
        var entityService2 = sp.GetService<IEntityService<Course, int>>();
        var entityService3 = sp.GetService<IEntityService<Course, int, CourseSearchObject>>();
        var entityService4 = sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var entityService5 = sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var manager1 = sp.GetService<IEntityManager<Course>>();
        var manager2 = sp.GetService<IEntityManager<Course, int>>();
        var manager3 = sp.GetService<IEntityManager<Course, int, CourseSearchObject>>();
        var manager4 = sp.GetService<IEntityManager<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>();
        var manager5 = sp.GetService<IEntityManager<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>();

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
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<Course, int, CourseIncludes>>());
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityProcessors, Is.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(entityReadService5, Is.TypeOf<EntityReadService<ContosoContext, Course, CourseSearchObject, CourseSortBy, CourseIncludes>>());
        Assert.That(primers.Length, Is.EqualTo(4));
        Assert.That(primers.OfType<HasCreatedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<HasLastModifiedDbPrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<ArchivablePrimer>(), Is.Not.Empty);
        Assert.That(primers.OfType<CoursePrimer>(), Is.Not.Empty);
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(repo1, Is.TypeOf<CourseRepository4>());
        Assert.That(repo2, Is.TypeOf<CourseRepository4>());
        Assert.That(repo3, Is.TypeOf<CourseRepository4>());
        Assert.That(repo4, Is.TypeOf<CourseRepository4>());
        Assert.That(repo5, Is.TypeOf<CourseRepository4>());
        Assert.That(entityService1, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService2, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService3, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService4, Is.TypeOf<CustomCourseService4>());
        Assert.That(entityService5, Is.TypeOf<CustomCourseService4>());
        Assert.That(manager1, Is.TypeOf<CourseManager4>());
        Assert.That(manager2, Is.TypeOf<CourseManager4>());
        Assert.That(manager3, Is.TypeOf<CourseManager4>());
        Assert.That(manager4, Is.TypeOf<CourseManager4>());
        Assert.That(manager5, Is.TypeOf<CourseManager4>());
    }
}