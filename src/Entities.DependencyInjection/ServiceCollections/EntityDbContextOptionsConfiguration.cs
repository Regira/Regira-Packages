using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.DAL.EFcore.Services;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.EFcore.Primers;

namespace Regira.Entities.DependencyInjection.ServiceCollections;

/// <summary>
/// Registered as an <b>open generic</b> <see cref="IDbContextOptionsConfiguration{TContext}"/>, so EF applies it
/// while building the options of <b>every</b> context registered via <c>AddDbContext</c>. It consults the
/// <see cref="DbContextWiringRegistry"/> (assignability match) and contributes the selected plumbing —
/// primer/normalizer/auto-truncate interceptors, the UTC date convention and the archived query filter.
/// Contexts not registered with <c>UseEntities()</c> (directly or via a base type) are left untouched, as is
/// a context constructed outside DI — it never consults the service collection at all.<br />
/// Because the match happens at options-build time, it works for abstract-base registrations
/// (<c>UseEntities&lt;AppContextBase&gt;()</c> + <c>AddDbContext&lt;SqlServerAppContext&gt;()</c>) and is
/// independent of `AddDbContext` ↔ `UseEntities()` call order.
/// </summary>
/// <typeparam name="TContext"></typeparam>
public class EntityDbContextOptionsConfiguration<TContext> : IDbContextOptionsConfiguration<TContext>
    where TContext : DbContext
{
    public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
    {
        var wiring = serviceProvider.GetService<DbContextWiringRegistry>()?.GetFor(typeof(TContext)) ?? DbContextWiring.None;
        if (wiring == DbContextWiring.None)
        {
            return;
        }

        // HasInterceptor guards keep repeated configurations from stacking duplicates
        if (wiring.HasFlag(DbContextWiring.PrimerInterceptors) && !optionsBuilder.HasInterceptor<EntityPrimerContainerInterceptor>())
        {
            optionsBuilder.AddInterceptors(new EntityPrimerContainerInterceptor(serviceProvider));
        }
        if (wiring.HasFlag(DbContextWiring.NormalizerInterceptors) && !optionsBuilder.HasInterceptor<EntityNormalizerContainerInterceptor>())
        {
            optionsBuilder.AddInterceptors(new EntityNormalizerContainerInterceptor(serviceProvider));
        }
        if (wiring.HasFlag(DbContextWiring.AutoTruncateInterceptors))
        {
            optionsBuilder.AddAutoTruncateInterceptors();
        }
        if (wiring.HasFlag(DbContextWiring.UtcDateTimeConvention))
        {
            optionsBuilder.AddUtcDateTimeConvention();
        }
        if (wiring.HasFlag(DbContextWiring.ArchivedQueryFilter))
        {
            optionsBuilder.AddArchivedQueryFilter();
        }
    }
}
