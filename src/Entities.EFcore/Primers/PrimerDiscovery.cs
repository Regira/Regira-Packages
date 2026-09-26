using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Utilities;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// The single primer-discovery used by BOTH save paths — the <see cref="EntityPrimerContainer"/>
/// (<c>ApplyPrimers()</c>, e.g. seeding scripts) and the <see cref="EntityPrimerContainerInterceptor"/>
/// (production <c>SaveChanges</c>). Only registrations made <em>as</em> the primer interface count —
/// <c>IEntityPrimer</c> or a closed <c>IEntityPrimer&lt;TEntity&gt;</c>; a primer registered under its own
/// concrete type (<c>AddTransient&lt;MyPrimer&gt;()</c>, for manual resolution) is not "a registered
/// primer" and must not auto-run. Each surviving descriptor is materialized <em>directly</em> from itself
/// (instance / factory / implementation type), never positionally against
/// <see cref="ServiceProviderServiceExtensions.GetServices"/> — otherwise instances contributed by an
/// open-generic <c>IEntityPrimer&lt;&gt;</c> registration misalign the pairing and the wrong primer runs.
/// <list type="bullet">
///   <item>dual registrations of the same implementation (<c>AddPrimer&lt;TEntity, TPrimer&gt;()</c>
///     registers typed + untyped) dedupe by implementation identity
///     (<c>ImplementationInstance</c> / <c>ImplementationFactory</c> / <c>ImplementationType</c>);</item>
///   <item>distinct lambda registrations (<c>e.Prime(...)</c>) have distinct factory delegates and all
///     survive — even though they share the closed <c>EntityPrimer&lt;TEntity&gt;</c> runtime type;</item>
///   <item>typed-only registrations (<c>IEntityPrimer&lt;TEntity&gt;</c> without the untyped interface)
///     are included, so they run on every save path.</item>
/// </list>
/// Order follows registration order across all service types.
/// </summary>
internal static class PrimerDiscovery
{
    public static IEntityPrimer[] GetPrimers(IServiceProvider serviceProvider, IEnumerable<ServiceDescriptor> descriptors)
        => HookDiscovery.GetHooks<IEntityPrimer>(serviceProvider, descriptors, typeof(IEntityPrimer<>));
}
