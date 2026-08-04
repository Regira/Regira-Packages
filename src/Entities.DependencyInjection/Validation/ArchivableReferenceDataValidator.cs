using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports an <see cref="IArchivable"/> entity that other <em>separately registered</em> entities reference
/// through a <b>required</b> foreign key — reference data behind a required FK, rather than an aggregate root.
/// <para>
/// Archiving such a row silently removes the dependents from list results. The archived query filter is a real
/// EF filter on the principal, so it propagates into <c>Include(...)</c>, and where the navigation is required
/// EF composes it as an inner join: the dependent rows drop out of the <c>items</c> projection. The
/// <c>count</c> query carries no includes, hence no join, hence no elimination — so <c>/search</c> reports a
/// total that its own page does not contain, and every page comes back short. Nothing errors and nothing logs.
/// </para>
/// <para>
/// This is the opposite of the aggregate-parent case (<c>Order</c> → <c>OrderLine</c>), where hiding an
/// archived parent's children IS the intent and the matching EF warning is expected noise. The two are told
/// apart by whether the dependent is separately registered: an <c>OrderLine</c> synced by its parent's
/// <c>Related()</c> is never queried on its own, while an <c>Asset</c> with its own list endpoint is. Only the
/// latter is reported. A dependent that already declares a query filter of its own is treated as handled — that
/// is the documented remedy for a dependent you query directly.
/// </para>
/// <para>
/// Silent on <c>net8.0</c>: EF Core 9 has no named query filters, so no archived filter is installed on the
/// principal at all there and archived rows are excluded at the root of the query instead. The include never
/// becomes a filtered inner join, so the divergence cannot occur.
/// </para>
/// </summary>
internal sealed class ArchivableReferenceDataValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
#if !NET10_0_OR_GREATER
        // see the class remarks: the propagating filter that causes this does not exist on EF Core 9
        _ = context;
        yield break;
#else
        // Entities with their own registration are the ones reachable by a list/search endpoint, so they are
        // the ones whose pages can come back short. A Related() child is written and read through its parent.
        var registered = context.Registrations.Entities.Select(e => e.EntityType).ToHashSet();
        var ownedByParent = context.Registrations.Related.Select(r => r.RelatedType).ToHashSet();

        foreach (var inspectType in InspectableContextTypes(context))
        {
            DbContext dbContext;
            try
            {
                dbContext = (DbContext)context.Provider.GetRequiredService(inspectType);
            }
            catch
            {
                // The archived-filter validator already reports an uninspectable context; saying it twice
                // adds noise without adding information.
                continue;
            }

            foreach (var issue in Hazards(dbContext, inspectType, registered, ownedByParent))
            {
                yield return issue;
            }
        }
#endif
    }

#if NET10_0_OR_GREATER
    private static IEnumerable<EntityValidationIssue> Hazards(DbContext dbContext, Type inspectType, HashSet<Type> registered, HashSet<Type> ownedByParent)
    {
        var principals = dbContext.Model.GetEntityTypes()
            .Where(t => typeof(IArchivable).IsAssignableFrom(t.ClrType) && t.BaseType == null && !t.IsOwned())
            .ToArray();

        foreach (var principal in principals)
        {
            var dependents = principal.GetReferencingForeignKeys()
                .Where(fk => fk.IsRequired && !fk.DeclaringEntityType.IsOwned())
                // A filter of the dependent's own is the documented remedy — it keeps count and items in
                // agreement by eliminating the same rows from both.
                .Where(fk => fk.DeclaringEntityType.GetDeclaredQueryFilters().Count == 0)
                .Where(fk => registered.Contains(fk.DeclaringEntityType.ClrType) && !ownedByParent.Contains(fk.DeclaringEntityType.ClrType))
                .GroupBy(fk => fk.DeclaringEntityType.ClrType)
                // The remedy is a predicate over a navigation, so it only compiles when the dependent has one.
                // A link entity mapped with a bare WithOne() — every EntityAttachment-derived type — has none.
                .Select(g => new { Type = g.Key, Navigation = g.Select(fk => fk.DependentToPrincipal?.Name).FirstOrDefault(n => n != null) })
                .OrderBy(d => d.Type.Name)
                .ToArray();

            if (dependents.Length == 0)
            {
                continue;
            }

            var names = string.Join(", ", dependents.Select(d => d.Type.Name));
            // One message, one worked remedy — so prescribe for the dependent that needs the *longer* fix.
            // Shown against a navigation-carrying dependent, the filter-only remedy silently under-serves a
            // navigationless sibling; the reverse is merely more verbose than that sibling needs.
            var target = dependents.FirstOrDefault(d => d.Navigation == null) ?? dependents[0];
            var first = target.Type.Name;
            // Each dependent carries its own filter, so a multi-dependent warning is not fixed by one line.
            var eachOne = dependents.Length > 1 ? $" Each of {names} needs its own, {first} shown here." : "";
            var navigation = target.Navigation ?? principal.ClrType.Name;
            var mirror = $"modelBuilder.Entity<{first}>().HasQueryFilter(x => !x.{navigation}!.IsArchived)";
            var remedy = target.Navigation != null
                ? $"mirror the filter on the dependent: {mirror} — that is what keeps count and items in agreement."
                : $"mirror the filter on the dependent — but {first} has no navigation back to {principal.ClrType.Name} for the predicate to bind to " +
                  $"(a link entity mapped with a bare WithOne() carries the foreign key only), so add one, re-point the relationship at it " +
                  $"(.WithOne(x => x.{navigation}!)), and then filter: {mirror} — that is what keeps count and items in agreement.";
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{principal.ClrType.Name} is IArchivable and is the required end of a relationship with separately registered {names} (in {inspectType.Name}). " +
                $"Archiving one {principal.ClrType.Name} hides the {first} rows that reference it from list results while /search still counts them, " +
                "so pages come back short and count disagrees with items. Nothing errors. " +
                $"ACTION: decide what {principal.ClrType.Name} is. Reference data (a lookup, a category, a status) should NOT be IArchivable — " +
                "delete it for real and let OnDelete(Restrict) return 409 while it is in use — or make the foreign key optional, " +
                $"so the dependent survives with a null navigation. If it really is an aggregate parent and hiding {first} is the intent, " +
                remedy + eachOne +
                " See entities.patterns → Soft Delete.");
        }
    }

    /// <summary>
    /// The concrete <see cref="DbContext"/> types to inspect — a recorded context type may be an abstract base,
    /// and the model EF actually builds belongs to the registered concrete type(s) it covers.
    /// </summary>
    private static IEnumerable<Type> InspectableContextTypes(EntityValidationContext context)
        => context.Registrations.ContextTypes
            .SelectMany(contextType => context.Services
                .Select(d => d.ServiceType)
                .Where(t => !t.IsAbstract && typeof(DbContext).IsAssignableFrom(t) && contextType.IsAssignableFrom(t))
                .DefaultIfEmpty(contextType))
            .Where(t => !t.IsAbstract)
            .Distinct();
#endif
}
