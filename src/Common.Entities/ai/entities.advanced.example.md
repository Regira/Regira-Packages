## Usings

**Usings**
```csharp no-compile
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Extensions;
using Regira.Entities.Keywords.Abstractions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.Mapster;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Normalizing;
using Regira.Normalizing.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
```

## Program.cs setup

**Program.cs**
```csharp no-compile
// Basic setup: follow the shared project setup guide — get_package("Regira.Setup", "project.setup") — for the selected template first.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ProjectsContext>(options =>
    // use DB provider at wish
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// add entity services and configure them
builder.Services.AddEntityServices();
```

## Entity models and search objects

**Models**
```csharp no-compile
public class Stakeholder : IEntity<Guid>, IHasCode, IHasTitle, IHasNormalizedContent
{
    public Guid Id { get; set; }
    [MaxLength(16)]
    public string? Code { get; set; }
    [Required, MaxLength(256)]
    public string? Title { get; set; }
    [MaxLength(256), Normalized(SourceProperties = [nameof(Code), nameof(Title)])]
    public string? NormalizedContent { get; set; }
    [NotMapped]
    public int NumberOfProjects { get; set; }

    public ICollection<Project>? Projects { get; set; }
}
public class Project : IEntityWithSerial, IHasCode, IHasDescription, IHasStartEndDate, IHasTimestamps, IArchivable, IHasNormalizedTitle, IHasNormalizedContent
{
    public int Id { get; set; }
    public Guid OwnerId { get; set; }
    [MaxLength(8)]
    public string? Code { get; set; }
    [Required, MaxLength(64)]
    public string? Title { get; set; }
    [Required, MaxLength(64)]
    public string? Slug { get; set; }
    [MaxLength(1024)]
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [MaxLength(128), Normalized(SourceProperties = [nameof(Code), nameof(Title)])]
    public string? NormalizedTitle { get; set; }
    [MaxLength(1024), Normalized(SourceProperties = [nameof(Code), nameof(Title), nameof(Description)])]
    public string? NormalizedContent { get; set; }

    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsArchived { get; set; }

    public Stakeholder? Owner { get; set; }
    public ICollection<ProjectTag>? Tags { get; set; }
    public ICollection<RelatedProject>? ParentEntities { get; set; }
    public ICollection<RelatedProject>? ChildEntities { get; set; }
}
public class RelatedProject : IEntityWithSerial, ISortable
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int ChildId { get; set; }
    public Project? Parent { get; set; }
    public Project? Child { get; set; }
    public int SortOrder { get; set; }
}
public class ProjectTag : IEntityWithSerial, IHasNormalizedTitle
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    [Required, MaxLength(64)]
    public string? Title { get; set; }
    [MaxLength(64), Normalized(SourceProperties = [nameof(Title)])]
    public string? NormalizedTitle { get; set; }

    public Project? Project { get; set; }
}

public enum StakeholderSortBy
{
    Default = 0,
    Title,
    NumberOfProjectsDesc,
}
public record ProjectSearchObject : SearchObject
{
    public string? Code { get; set; }
    public string? Title { get; set; }
    public bool? IsRoot { get; set; }
    public ICollection<Guid>? OwnerIds { get; set; }
    public ICollection<int>? ParentIds { get; set; }
    public ICollection<int>? ChildIds { get; set; }
    public DateTime? MinStartDate { get; set; }
    public DateTime? MaxStartDate { get; set; }
    public DateTime? MinEndDate { get; set; }
    public DateTime? MaxEndDate { get; set; }
    public DateTime? Date { get; set; }
}
public enum ProjectSortBy
{
    Default = 0,
    Title,
    Id,
    IdDesc,
    StartDate,
    StartDateDesc
}
[Flags]
public enum ProjectIncludes
{
    None = 0,
    Stakeholder = 1 << 0,
    ParentEntities = 1 << 1,
    ChildEntities = 1 << 2
}
```

## DbContext configuration

**DbContext**
```csharp no-compile
public class ProjectsContext(DbContextOptions<ProjectsContext> options) : DbContext(options)
{
    public DbSet<Stakeholder> Stakeholders { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<RelatedProject> RelatedProjects { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Stakeholder>(o =>
        {
            o.HasMany(s => s.Projects).WithOne(p => p.Owner).HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Project>(o =>
        {
            o.HasMany(e => e.Tags).WithOne(t => t.Project).HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RelatedProject>(o =>
        {
            o.HasOne(rp => rp.Parent).WithMany(p => p.ChildEntities).HasForeignKey(rp => rp.ParentId).OnDelete(DeleteBehavior.Restrict);
            o.HasOne(rp => rp.Child).WithMany(p => p.ParentEntities).HasForeignKey(rp => rp.ChildId).OnDelete(DeleteBehavior.Restrict);
        });

        // Project is IArchivable — nothing to add: UseEntities<TContext>(e => e.UseDefaults())
        // wires the archived query filter into the context's options
    }
}
```

## DTOs and input DTOs

**DTOs**
```csharp
public class StakeholderDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? Title { get; set; }
    public int? NumberOfProjects { get; set; }
}
public class StakeholderInputDto
{
    public Guid Id { get; set; }
    [MaxLength(16)]
    public string? Code { get; set; }
    [Required, MaxLength(256)]
    public string? Title { get; set; }
}
public class RelatedProjectDto
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int ChildId { get; set; }
    public ProjectDto? Parent { get; set; }
    public ProjectDto? Child { get; set; }
    public int SortOrder { get; set; }
}
public class RelatedProjectInputDto
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int ChildId { get; set; }
}
public class ProjectTagDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
}
public class ProjectTagInputDto
{
    public int Id { get; set; }
    [Required, MaxLength(64)]
    public string? Title { get; set; }
}
public class ProjectDto
{
    public int Id { get; set; }
    public Guid OwnerId { get; set; }
    public string? Code { get; set; }
    public string? Title { get; set; }
    public string? Uri { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }

    public StakeholderDto? Owner { get; set; }
    public ICollection<ProjectTagDto>? Tags { get; set; }
    public ICollection<RelatedProjectDto>? ParentEntities { get; set; }
    public ICollection<RelatedProjectDto>? ChildEntities { get; set; }
}
public class ProjectInputDto
{
    public int Id { get; set; }
    public Guid OwnerId { get; set; }
    [MaxLength(8)]
    public string? Code { get; set; }
    [Required, MaxLength(64)]
    public string? Title { get; set; }
    [MaxLength(1024)]
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public ICollection<ProjectTagInputDto>? Tags { get; set; }
    public ICollection<RelatedProjectInputDto>? ParentEntities { get; set; }
    public ICollection<RelatedProjectInputDto>? ChildEntities { get; set; }
}
```

## Helper services (processor, primer, query builders, normalizer, manager)

**Helper Services**
```csharp no-compile
public class StakeholderProcessor(ProjectsContext dbContext) : IEntityProcessor<Stakeholder, EntityIncludes>
{
    public async Task Process(IList<Stakeholder> items, EntityIncludes? includes, CancellationToken token = default)
    {
        var ids = items.Select(io => io.Id).ToList();
        var ownersWithNumberOfProjects = await dbContext.Set<Project>()
            .Where(p => ids.Contains(p.OwnerId))
            .GroupBy(p => p.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count);
        foreach (var item in items)
            if (ownersWithNumberOfProjects.TryGetValue(item.Id, out var numberOfProjects))
                item.NumberOfProjects = numberOfProjects;
    }
}
public class StakeholderPrimer : EntityPrimerBase<Stakeholder>
{
    public override Task PrepareAsync(Stakeholder entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();

        return Task.CompletedTask;
    }
}
public class ProjectFilteredQueryBuilder(IQKeywordHelper qHelper) : IFilteredQueryBuilder<Project, int, ProjectSearchObject>
{
    public IQueryable<Project> Build(IQueryable<Project> query, ProjectSearchObject? so)
    {
        if (so != null)
        {
            if (so.IsRoot.HasValue)
                query = query.Where(x => so.IsRoot.Value == !x.ParentEntities!.Any());
            if (!string.IsNullOrEmpty(so.Code))
                query = query.Where(x => x.Code != null && x.Code.Contains(so.Code));
            if (!string.IsNullOrWhiteSpace(so.Title))
                query = qHelper.Parse(so.Title)
                    .Aggregate(
                        query,
                        (current, keyword) => keyword.HasWildcard
                            ? current.Where(x => EF.Functions.Like(x.NormalizedTitle, keyword.Q))
                            : current.Where(x => x.NormalizedTitle == keyword.Normalized)
                    );
            if (so.MinEndDate.HasValue)
                query = query.Where(x => x.EndDate >= so.MinEndDate.Value);
            if (so.MaxEndDate.HasValue)
                query = query.Where(x => x.EndDate <= so.MaxEndDate.Value);
            if (so.Date.HasValue)
                query = query.Where(x => (x.StartDate <= so.Date.Value && x.EndDate >= so.Date.Value) || (x.StartDate <= so.Date.Value && x.EndDate == null) || (x.StartDate == null && x.EndDate >= so.Date.Value));
            if (so.OwnerIds?.Any() == true)
                query = query.Where(x => so.OwnerIds.Contains(x.OwnerId));
            if (so.ParentIds != null && so.ParentIds.Any())
                query = query.Where(x => x.ParentEntities!.Any(pe => so.ParentIds.Contains(pe.ParentId)));
            if (so.ChildIds != null && so.ChildIds.Any())
                query = query.Where(x => x.ChildEntities!.Any(ce => so.ChildIds.Contains(ce.ChildId)));
        }

        return query;
    }
}
public class ProjectSortingQueryBuilder : ISortedQueryBuilder<Project, int, ProjectSortBy>
{
    // OrderOrThenBy (Regira.Entities.EFcore.Extensions) starts or continues the ordering — SortBy runs
    // once per requested sort value, so the query may or may not be ordered already.
    public IQueryable<Project> SortBy(IQueryable<Project> query, ProjectSortBy? sortBy = null)
        => sortBy switch
        {
            ProjectSortBy.Title => query.OrderOrThenBy(x => x.Title),
            ProjectSortBy.StartDate => query.OrderOrThenBy(x => x.StartDate),
            ProjectSortBy.StartDateDesc => query.OrderOrThenByDescending(x => x.StartDate),
            _ => query.OrderOrThenBy(x => x.Id)
        };
}
public class ProjectIncludingQueryBuilder : IIncludableQueryBuilder<Project, int, ProjectIncludes>
{
    public IQueryable<Project> AddIncludes(IQueryable<Project> query, ProjectIncludes? includes = null)
    {
        if (includes.HasValue)
        {
            if (includes.Value.HasFlag(ProjectIncludes.Stakeholder))
                query = query.Include(x => x.Owner);
            if (includes.Value.HasFlag(ProjectIncludes.ParentEntities))
                query = query.Include(x => x.ParentEntities!).ThenInclude(pe => pe.Parent);
            if (includes.Value.HasFlag(ProjectIncludes.ChildEntities))
                query = query.Include(x => x.ChildEntities!).ThenInclude(ce => ce.Child);
        }
        return query;
    }
}
public class ProjectPrepper : EntityPrepperBase<Project>
{
    public override Task Prepare(Project modified, Project? original, CancellationToken token = default)
    {
        modified.Slug ??= modified.Title?.ToLowerInvariant().Replace(' ', '-');
        return Task.CompletedTask;
    }
}
public class ProjectNormalizer(ProjectsContext dbContext, INormalizer? normalizer = null) : EntityNormalizerBase<Project>(normalizer)
{
    public override bool IsExclusive => true;
    private List<Stakeholder> _owners = null!;

    public override async Task HandleNormalizeMany(IEnumerable<Project> items, CancellationToken token = default)
    {
        var itemList = items as Project[] ?? items.ToArray();
        var ownerIds = itemList.Select(x => x.OwnerId).Distinct().ToList();
        var pendingStakeholders = dbContext.ChangeTracker.Entries<Stakeholder>()
            .Where(e => (e.State == EntityState.Added || e.State == EntityState.Modified) && ownerIds.Contains(e.Entity.Id))
            .Select(e => e.Entity)
            .ToDictionary(e => e.Id);

        var dbOwnerIds = ownerIds.Where(id => !pendingStakeholders.ContainsKey(id)).ToList();
        var dbStakeholders = dbOwnerIds.Count > 0
            ? await dbContext.Set<Stakeholder>().Where(o => dbOwnerIds.Contains(o.Id)).ToListAsync(token)
            : [];

        _owners = [.. pendingStakeholders.Values, .. dbStakeholders];

        await base.HandleNormalizeMany(itemList, token);
    }
    public override Task HandleNormalize(Project item, CancellationToken token = default)
    {
        var contentEntries = new List<string?> { item.Code, item.Title, item.Description };

        var owner = _owners.Find(x => x.Id == item.OwnerId);
        if (owner != null)
        {
            contentEntries.AddRange([owner.Code, owner.Title]);
        }

        item.NormalizedContent = string.Join(' ', contentEntries.Where(x => !string.IsNullOrWhiteSpace(x)));

        return Task.CompletedTask;
    }
}
public class ProjectAfterMapper(/*ILinkGenerator linkGenerator*/) : EntityAfterMapperBase<Project, ProjectDto>
{
    public override void AfterMap(Project source, ProjectDto target)
        => target.Uri = $"BASE_PATH/{source.Slug}";
}
public class ProjectManager(IEntityRepository<Project, ProjectSearchObject, ProjectSortBy, ProjectIncludes> service)
    : EntityWrappingServiceBase<Project, ProjectSearchObject, ProjectSortBy, ProjectIncludes>(service), IEntityService<Project, ProjectSearchObject, ProjectSortBy, ProjectIncludes>
{
    public override Task Add(Project item, CancellationToken token = default)
    {
        Validate(item);
        return base.Add(item, token);
    }
    public override Task<Project?> Modify(Project item, CancellationToken token = default)
    {
        Validate(item);
        return base.Modify(item, token);
    }
    // No Save override needed: base Save() routes to Add()/Modify() above (the write path calls Save()).

    public void Validate(Project item)
    {
        if (item.ParentEntities?.Any(p => item.ChildEntities?.Any(c => c.ChildId == p.ParentId) == true) == true)
            throw new EntityInputException<Project>("A project cannot be both parent and child of the same project.") { Item = item };
    }
}
```

## Service registration extensions

**Configuration Extensions**
```csharp no-compile
public static class EntityServiceCollectionExtensions
{
    public static EntityServiceCollection<ProjectsContext> AddEntityServices(this IServiceCollection services)
        => services
            .UseEntities<ProjectsContext>(o =>
            {
                o.UseDefaults();
                o.UseMapsterMapping();
            })
            .AddStakeholders()
            .AddProjects();
}
public static class StakeholderServiceCollectionExtensions
{
    public static EntityServiceCollection<ProjectsContext> AddStakeholders(this IEntityServiceCollection<ProjectsContext> services)
        => services
            .For<Stakeholder, Guid, SearchObject<Guid>, StakeholderSortBy, EntityIncludes>(e =>
            {
                e.SortBy((query, sortBy) =>
                {
                    return sortBy switch
                    {
                        StakeholderSortBy.Title => query.OrderBy(x => x.Title),
                        StakeholderSortBy.NumberOfProjectsDesc => query.OrderByDescending(x => x.Projects!.Count),
                        _ => query.OrderBy(x => x.Id)
                    };
                });

                e.AddProcessor<StakeholderProcessor>();
                e.AddPrimer<StakeholderPrimer>();
            });
}
public static class ProjectServiceCollectionExtensions
{
    public static EntityServiceCollection<ProjectsContext> AddProjects(this IEntityServiceCollection<ProjectsContext> services)
        => services
            .For<Project, ProjectSearchObject, ProjectSortBy, ProjectIncludes>(e =>
            {
                e.AddFilter<ProjectFilteredQueryBuilder>();
                e.AddSortBy<ProjectSortingQueryBuilder>();
                e.AddIncludes<ProjectIncludingQueryBuilder>();

                e.UseEntityService<ProjectManager>();

                e.Related(item => item.Tags);
                e.Related(item => item.ParentEntities, item => item.ParentEntities?.SetSortOrder());
                e.Related(item => item.ChildEntities, item => item.ChildEntities?.SetSortOrder());
                // configure overload — nest sub-collections or add per-item prepare via RelatedEntityBuilder
                // e.Related(item => item.OrderItems, configure: builder =>
                // {
                //     builder.Related(oi => oi.Options);       // sync OrderItem.Options sub-collection
                //     builder.Prepare(oi => oi.SetDefaults()); // run per-item logic on each OrderItem
                // });

                e.AddPrepper<ProjectPrepper>();
                e.AddNormalizer<ProjectNormalizer>();
                e.UseMapping<ProjectDto, ProjectInputDto>()
                    .After<ProjectAfterMapper>();
            });
}
```

## API controllers

**Controllers**
```csharp no-compile
[ApiController, Route("projects")] // For<Project, ProjectSearchObject, ProjectSortBy, ProjectIncludes>
public class ProjectController : EntityControllerBase<Project, ProjectSearchObject, ProjectSortBy, ProjectIncludes, ProjectDto, ProjectInputDto>;

[ApiController, Route("stakeholders")] // For<Stakeholder, Guid, SearchObject<Guid>, StakeholderSortBy, EntityIncludes>
public class StakeholderController : EntityControllerBase<Stakeholder, Guid, SearchObject<Guid>, StakeholderSortBy, EntityIncludes, StakeholderDto, StakeholderInputDto>;
```
