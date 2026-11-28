using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators.Abstractions;

namespace Entities.DependencyInjection.Testing.Infrastructure;

// An app-wide write service: one class, closed over each registration's context, entity and key
public class AppWriteService<TContext, TEntity, TKey>(
    TContext dbContext,
    IEntityReadService<TEntity, TKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntity, TKey>(dbContext, readService, preppers, validators, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>;

// Misfits
/// <summary>Admits only entities with attachments.</summary>
public class AttachmentOwnerWriteService<TContext, TEntity, TKey>(
    TContext dbContext,
    IEntityReadService<TEntity, TKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : AppWriteService<TContext, TEntity, TKey>(dbContext, readService, preppers, validators, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>, IHasAttachments;

/// <summary>The int-keyed shape: 2 type parameters, where a write service type has 3.</summary>
public class IntKeyedWriteService<TContext, TEntity>(
    TContext dbContext,
    IEntityReadService<TEntity, int> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntity>(dbContext, readService, preppers, validators, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<int>;

/// <summary>Type parameters out of EntityWriteService's order: the entity first.</summary>
public class SwappedWriteService<TEntity, TContext, TKey>(
    TContext dbContext,
    IEntityReadService<TEntity, TKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntity, TKey>(dbContext, readService, preppers, validators, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>;
