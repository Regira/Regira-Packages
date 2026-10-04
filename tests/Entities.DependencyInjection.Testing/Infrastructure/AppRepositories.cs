using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.DependencyInjection.Testing.Infrastructure;

// An app-wide repository, one class per EntityRepository shape
public class AppRepository<TEntity>(IEntityReadService<TEntity, int, SearchObject<int>> readService, IEntityWriteService<TEntity, int> writeService)
    : AppRepository<TEntity, int>(readService, writeService), IEntityRepository<TEntity>
    where TEntity : class, IEntity<int>;

public class AppRepository<TEntity, TKey>(IEntityReadService<TEntity, TKey, SearchObject<TKey>> readService, IEntityWriteService<TEntity, TKey> writeService)
    : EntityRepository<TEntity, TKey>(readService, writeService)
    where TEntity : class, IEntity<TKey>;

public class AppRepository<TEntity, TKey, TSearchObject>(IEntityReadService<TEntity, TKey, TSearchObject> readService, IEntityWriteService<TEntity, TKey> writeService)
    : EntityRepository<TEntity, TKey, TSearchObject>(readService, writeService)
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new();

public class AppRepository<TEntity, TSearchObject, TSortBy, TIncludes>(
    IEntityReadService<TEntity, int, TSearchObject, TSortBy, TIncludes> readService, IEntityWriteService<TEntity, int> writeService)
    : AppRepository<TEntity, int, TSearchObject, TSortBy, TIncludes>(readService, writeService), IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

public class AppRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
    IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes> readService, IEntityWriteService<TEntity, TKey> writeService)
    : EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(readService, writeService)
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

// Misfits
/// <summary>Admits only entities with attachments.</summary>
public class AttachmentOwnerRepository<TEntity>(IEntityReadService<TEntity, int, SearchObject<int>> readService, IEntityWriteService<TEntity, int> writeService)
    : AppRepository<TEntity>(readService, writeService)
    where TEntity : class, IEntity<int>, IHasAttachments;

/// <summary>A 1-parameter repository missing <see cref="IEntityRepository{TEntity}"/>.</summary>
public class IntKeyedRepository<TEntity>(IEntityReadService<TEntity, int, SearchObject<int>> readService, IEntityWriteService<TEntity, int> writeService)
    : EntityRepository<TEntity, int>(readService, writeService)
    where TEntity : class, IEntity<int>;

public class NotARepository<TEntity>;
