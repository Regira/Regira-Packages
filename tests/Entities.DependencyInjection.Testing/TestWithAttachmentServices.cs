using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestWithAttachmentServices
{
    [Test]
    public void Without_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions()))
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Attachment, int, AttachmentSearchObject<int>>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Attachment, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Attachment, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Attachment, int, AttachmentSearchObject<int>, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Attachment, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Attachment, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Attachment, int, AttachmentSearchObject<int>>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Attachment, int>>();
        var repo2 = sp.GetService<IEntityRepository<Attachment, int>>();
        var repo3 = sp.GetService<IEntityRepository<Attachment, int, AttachmentSearchObject<int>>>();
        var entityService2 = sp.GetService<IEntityService<Attachment, int>>();
        var entityService3 = sp.GetService<IEntityService<Attachment, int, AttachmentSearchObject<int>>>();

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters.First(), Is.TypeOf<AttachmentFilteredQueryBuilder<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(entityProcessors.Length, Is.EqualTo(1));
        Assert.That(entityProcessors.Last(), Is.TypeOf<AttachmentProcessor<Attachment, int>>());
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(primers.Length, Is.EqualTo(2));
        Assert.That(primers.OfType<AttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<EntityAttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Attachment, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
    }

    [Test]
    public void Without_Defaults3B()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .WithAttachments<Attachment, int, AttachmentSearchObject>(_ => new BinaryFileService(new FileSystemOptions()))
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Attachment, int, AttachmentSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Attachment, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Attachment, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Attachment, int, AttachmentSearchObject, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Attachment, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Attachment, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Attachment, int, AttachmentSearchObject>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Attachment, int>>();
        var repo2 = sp.GetService<IEntityRepository<Attachment, int>>();
        var repo3 = sp.GetService<IEntityRepository<Attachment, int, AttachmentSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<Attachment, int>>();
        var entityService3 = sp.GetService<IEntityService<Attachment, int, AttachmentSearchObject>>();

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters.First(), Is.TypeOf<AttachmentFilteredQueryBuilder<Attachment, int, AttachmentSearchObject>>());
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Attachment, int, AttachmentSearchObject>>());
        Assert.That(entityProcessors.Length, Is.EqualTo(1));
        Assert.That(entityProcessors.OfType<AttachmentProcessor<Attachment, int>>(), Is.Not.Empty);
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Attachment, int, AttachmentSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Attachment, int, AttachmentSearchObject>>());
        Assert.That(primers.Length, Is.EqualTo(2));
        Assert.That(primers.OfType<AttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<EntityAttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Attachment, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject>>());
    }

    [Test]
    public void With_QueryFilter()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions()), e =>
            {
                e.Filter((query, so) =>
                {
                    if (!string.IsNullOrWhiteSpace(so?.Q))
                    {
                        query = query.Where(x => x.FileName!.Contains(so.Q));
                    }
                    return query;
                });
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<Attachment, int, AttachmentSearchObject<int>>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<Attachment, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<Attachment, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<Attachment, int, AttachmentSearchObject<int>, EntitySortBy, EntityIncludes>>();
        var entityProcessors = sp.GetServices<IEntityProcessor<Attachment, EntityIncludes>>().ToArray();
        var entityReadService2 = sp.GetService<IEntityReadService<Attachment, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<Attachment, int, AttachmentSearchObject<int>>>();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<Attachment, int>>();
        var repo2 = sp.GetService<IEntityRepository<Attachment, int>>();
        var repo3 = sp.GetService<IEntityRepository<Attachment, int, AttachmentSearchObject<int>>>();
        var entityService2 = sp.GetService<IEntityService<Attachment, int>>();
        var entityService3 = sp.GetService<IEntityService<Attachment, int, AttachmentSearchObject<int>>>();

        Assert.That(queryFilters.OfType<AttachmentFilteredQueryBuilder<Attachment, int, AttachmentSearchObject<int>>>().Count, Is.EqualTo(1));
        Assert.That(queryFilters.OfType<EntityQueryFilter<Attachment, int, AttachmentSearchObject<int>>>().Count, Is.EqualTo(1));

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(sortableBuilder, Is.Null);
        Assert.That(includableBuilder, Is.Null);
        Assert.That(entityProcessors.Length, Is.EqualTo(1));
        Assert.That(entityProcessors.OfType<AttachmentProcessor<Attachment, int>>(), Is.Not.Empty);
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(primers.Length, Is.EqualTo(2));
        Assert.That(primers.OfType<AttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<EntityAttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(entityWriteService, Is.TypeOf<EntityWriteService<ContosoContext, Attachment, int>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<Attachment, int, AttachmentSearchObject<int>>>());
    }
}