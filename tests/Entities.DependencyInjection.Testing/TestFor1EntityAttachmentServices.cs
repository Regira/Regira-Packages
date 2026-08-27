using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestFor1EntityAttachmentServices
{
    [Test]
    public void Without_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions()))
            .For<Course>(e =>
            {
                e.HasAttachments(item => item.Attachments);
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<CourseAttachment, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<CourseAttachment, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject, EntitySortBy, EntityIncludes>>();
        var entityReadService2 = sp.GetService<IEntityReadService<CourseAttachment, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<CourseAttachment, int, EntityAttachmentSearchObject>>();
        var entityPreppers = sp.GetServices<IEntityPrepper>().ToArray();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<CourseAttachment, int>>();
        var repo2 = sp.GetService<IEntityRepository<CourseAttachment, int>>();
        var repo3 = sp.GetService<IEntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<CourseAttachment, int>>();
        var entityService3 = sp.GetService<IEntityService<CourseAttachment, int, EntityAttachmentSearchObject>>();

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        Assert.That(queryFilters.Length, Is.EqualTo(1));
        Assert.That(queryFilters.OfType<EntityAttachmentFilteredQueryBuilder<int, CourseAttachment, int, EntityAttachmentSearchObject, int, Attachment>>().Count, Is.EqualTo(1));
        // HasAttachments() ships a default sort on SortOrder: the per-owner services are registered here, so
        // there is no .For<CourseAttachment>() a consumer could hang one on — and paging applies a Take on
        // every request, which without an OrderBy is both an unfixable EF warning and an arbitrary order.
        Assert.That(sortableBuilder, Is.TypeOf<SortedQueryBuilder<CourseAttachment, int>>());
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<CourseAttachment, int>>());
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityPreppers.Length, Is.EqualTo(3));
        Assert.That(entityPreppers.OfType<EntityPrepper<Course>>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<RelatedAttachmentsPrepper<ContosoContext, Course, CourseAttachment, int, int, int, Attachment>>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<EntityAttachmentPrepper<ContosoContext, CourseAttachment, int, int, int, Attachment>>().Count(), Is.EqualTo(1));
        Assert.That(primers.Length, Is.EqualTo(2));
        Assert.That(primers.OfType<AttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<EntityAttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(entityWriteService, Is.TypeOf<EntityAttachmentWriteService<ContosoContext, CourseAttachment, int, int, int, Attachment>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
    }

    [Test]
    public void With_Defaults()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(e => e.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions()))
            .For<Course>(e =>
            {
                e.HasAttachments(item => item.Attachments);
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<CourseAttachment, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<CourseAttachment, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject, EntitySortBy, EntityIncludes>>();
        var entityReadService2 = sp.GetService<IEntityReadService<CourseAttachment, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<CourseAttachment, int, EntityAttachmentSearchObject>>();
        var entityPreppers = sp.GetServices<IEntityPrepper>().ToArray();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<CourseAttachment, int>>();
        var repo2 = sp.GetService<IEntityRepository<CourseAttachment, int>>();
        var repo3 = sp.GetService<IEntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<CourseAttachment, int>>();
        var entityService3 = sp.GetService<IEntityService<CourseAttachment, int, EntityAttachmentSearchObject>>();

        Assert.That(entityNormalizer, Is.TypeOf<DefaultEntityNormalizer>());
        Assert.That(globalFilters, Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterIdsQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterArchivablesQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasCreatedQueryBuilder<int>>(), Is.Not.Empty);
        Assert.That(globalFilters.OfType<FilterHasLastModifiedQueryBuilder<int>>(), Is.Not.Empty);

        Assert.That(primers.Length, Is.EqualTo(5));
        Assert.That(primers.OfType<HasCreatedDbPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<HasLastModifiedDbPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<ArchivablePrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<AttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<EntityAttachmentPrimer>().Count(), Is.EqualTo(1));

        Assert.That(queryFilters.Length, Is.EqualTo(1));
        Assert.That(queryFilters.OfType<EntityAttachmentFilteredQueryBuilder<int, CourseAttachment, int, EntityAttachmentSearchObject, int, Attachment>>().Count, Is.EqualTo(1));
        // HasAttachments() ships a default sort on SortOrder: the per-owner services are registered here, so
        // there is no .For<CourseAttachment>() a consumer could hang one on — and paging applies a Take on
        // every request, which without an OrderBy is both an unfixable EF warning and an arbitrary order.
        Assert.That(sortableBuilder, Is.TypeOf<SortedQueryBuilder<CourseAttachment, int>>());
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<CourseAttachment, int>>());
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityPreppers.Length, Is.EqualTo(4));
        Assert.That(entityPreppers.OfType<AutoServerOwnedPrepper>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<EntityPrepper<Course>>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<RelatedAttachmentsPrepper<ContosoContext, Course, CourseAttachment, int, int, int, Attachment>>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<EntityAttachmentPrepper<ContosoContext, CourseAttachment, int, int, int, Attachment>>().Count(), Is.EqualTo(1));
        Assert.That(entityWriteService, Is.TypeOf<EntityAttachmentWriteService<ContosoContext, CourseAttachment, int, int, int, Attachment>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
    }

    [Test]
    public void With_QueryFilter()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>()
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions()))
            .For<Course>(e =>
            {
                e.HasAttachments(item => item.Attachments, a =>
                {
                    a.Filter((query, so) =>
                    {
                        if (!string.IsNullOrWhiteSpace(so?.Q))
                        {
                            query = query.Where(x => x.Attachment!.FileName!.Contains(so.Q));
                        }
                        return query;
                    });
                });
            })
            .BuildServiceProvider();

        var entityNormalizer = sp.GetService<IEntityNormalizer>();
        var globalFilters = sp.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        var queryFilters = sp.GetServices<IFilteredQueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject>>().ToArray();
        var sortableBuilder = sp.GetService<ISortedQueryBuilder<CourseAttachment, int>>();
        var includableBuilder = sp.GetService<IIncludableQueryBuilder<CourseAttachment, int>>();
        var queryBuilder = sp.GetService<IQueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject, EntitySortBy, EntityIncludes>>();
        var entityReadService2 = sp.GetService<IEntityReadService<CourseAttachment, int>>();
        var entityReadService3 = sp.GetService<IEntityReadService<CourseAttachment, int, EntityAttachmentSearchObject>>();
        var entityPreppers = sp.GetServices<IEntityPrepper>().ToArray();
        var primers = sp.GetServices<IEntityPrimer>().ToArray();
        var entityWriteService = sp.GetService<IEntityWriteService<CourseAttachment, int>>();
        var repo2 = sp.GetService<IEntityRepository<CourseAttachment, int>>();
        var repo3 = sp.GetService<IEntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>();
        var entityService2 = sp.GetService<IEntityService<CourseAttachment, int>>();
        var entityService3 = sp.GetService<IEntityService<CourseAttachment, int, EntityAttachmentSearchObject>>();

        Assert.That(queryFilters.Length, Is.EqualTo(2));
        Assert.That(queryFilters.OfType<EntityQueryFilter<CourseAttachment, int, EntityAttachmentSearchObject>>().Count(), Is.EqualTo(1));
        Assert.That(queryFilters.OfType<EntityAttachmentFilteredQueryBuilder<int, CourseAttachment, int, EntityAttachmentSearchObject, int, Attachment>>().Count, Is.EqualTo(1));

        Assert.That(entityNormalizer, Is.Null);
        Assert.That(globalFilters, Is.Empty);
        // HasAttachments() ships a default sort on SortOrder: the per-owner services are registered here, so
        // there is no .For<CourseAttachment>() a consumer could hang one on — and paging applies a Take on
        // every request, which without an OrderBy is both an unfixable EF warning and an arbitrary order.
        Assert.That(sortableBuilder, Is.TypeOf<SortedQueryBuilder<CourseAttachment, int>>());
        Assert.That(includableBuilder, Is.TypeOf<IncludableQueryBuilder<CourseAttachment, int>>());
        Assert.That(queryBuilder, Is.TypeOf<QueryBuilder<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityReadService2, Is.TypeOf<EntityReadService<ContosoContext, CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityReadService3, Is.TypeOf<EntityReadService<ContosoContext, CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(primers.Length, Is.EqualTo(2));
        Assert.That(primers.OfType<AttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(primers.OfType<EntityAttachmentPrimer>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.Length, Is.EqualTo(3));
        Assert.That(entityPreppers.OfType<EntityPrepper<Course>>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<RelatedAttachmentsPrepper<ContosoContext, Course, CourseAttachment, int, int, int, Attachment>>().Count(), Is.EqualTo(1));
        Assert.That(entityPreppers.OfType<EntityAttachmentPrepper<ContosoContext, CourseAttachment, int, int, int, Attachment>>().Count(), Is.EqualTo(1));
        Assert.That(entityWriteService, Is.TypeOf<EntityAttachmentWriteService<ContosoContext, CourseAttachment, int, int, int, Attachment>>());
        Assert.That(repo2, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(repo3, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityService2, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
        Assert.That(entityService3, Is.TypeOf<EntityRepository<CourseAttachment, int, EntityAttachmentSearchObject>>());
    }
}