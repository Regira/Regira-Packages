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

        foreach (var (inspectType, dbContext) in context.DbContexts)
        {
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
                .Select(fk => fk.DeclaringEntityType.ClrType.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray();

            if (dependents.Length == 0)
            {
                continue;
            }

            var principalName = principal.ClrType.Name;
            var names = string.Join(", ", dependents);
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{principalName} is IArchivable and is the required end of a relationship with separately registered {names} (in {inspectType.Name}). " +
                $"Archiving one {principalName} hides the rows of {names} that reference it from list results while /search still counts them, " +
                "so pages come back short and count disagrees with items. Nothing errors. " +
                $"ACTION: decide what {principalName} is: reference data that should not be IArchivable, a foreign key that should be optional, " +
                "or an aggregate parent whose dependents mirror its filter. See entities.patterns → Soft Delete.");
        }
    }

#endif
}
