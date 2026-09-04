using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports an <see cref="IArchivable"/> entity type whose model carries no archived query filter, whichever
/// route was supposed to install it: the options wiring
/// (<c>DbContextWiring.ArchivedQueryFilter</c>, on by default via <c>UseDefaults()</c>) or an explicit
/// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> in <c>OnModelCreating</c>. It inspects the model
/// EF actually built, so it reports the outcome rather than the call.
/// <para>
/// Without the filter <c>DELETE</c> still flags the row but nothing hides it, so archived rows come back from
/// every list, every <c>GET /{id}</c> and every included collection. Nothing else catches it — the model is
/// valid, DI is satisfied, and the app compiles and starts clean, which is why it is an error rather than a
/// warning. What reaches it now is a context the wiring misses: a non-generic <c>UseEntities()</c>, or
/// <c>WireDbContext(...)</c> without the flag.
/// </para>
/// <para>
/// Blind spot by construction: a <c>DbContext</c> constructed outside DI
/// (<c>new AppDbContext(options)</c> — tests, design-time factories, seeding tools) builds its own model that
/// this validator never sees, and the options wiring never reaches. Such a context needs
/// <c>AddArchivedQueryFilter()</c> on its options builder, or the explicit <c>OnModelCreating</c> call.
/// </para>
/// <para>
/// Silent on <c>net8.0</c>: EF Core 9 has no named query filters, so no archived query filter is installed
/// there at all and archived rows are excluded by <c>FilterArchivablesQueryBuilder</c> at the root of the
/// query instead. There is nothing to be missing, and reporting it would be a false positive on every app.
/// </para>
/// </summary>
internal sealed class ArchivedQueryFilterValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
#if !NET10_0_OR_GREATER
        // see the class remarks: no archived query filter exists on EF Core 9, by design
        _ = context;
        yield break;
#else
        // An app that deliberately returns archived rows by default has nothing to hide, so a missing filter
        // is its configuration rather than a defect.
        if (ResolveDefaultArchivedFilter(context) == ArchivedFilter.Included)
        {
            yield break;
        }

        using var scope = context.Provider.CreateScope();
        foreach (var inspectType in ValidationContextTypes.Inspectable(context))
        {
            var (unfiltered, inspectionFailure) = UnfilteredArchivables(scope.ServiceProvider, inspectType);
            if (inspectionFailure != null)
            {
                yield return new EntityValidationIssue(EntityValidationSeverity.Info,
                    $"Could not inspect {inspectType.Name} for the archived query filter: {inspectionFailure}");
                continue;
            }
            if (unfiltered.Length == 0)
            {
                continue;
            }

            yield return new EntityValidationIssue(EntityValidationSeverity.Error,
                $"IArchivable without a soft-delete filter in {inspectType.Name}: {string.Join(", ", unfiltered)}. " +
                "DELETE flags these rows as archived, but nothing hides them: they keep coming back from every list, " +
                "from GET /{id} and from inside included collections. " +
                $"ACTION: register the context with UseEntities<{inspectType.Name}>(o => o.UseDefaults()) — that wires " +
                "the filter into the context's options (DbContextWiring.ArchivedQueryFilter), no DbContext change needed. " +
                "It is missing here because the wiring never reached this context: a non-generic UseEntities(), or " +
                "WireDbContext(...) without that flag. To wire the DbContext yourself instead, end " +
                $"{inspectType.Name}.OnModelCreating with modelBuilder.SetArchivedQueryFilter() " +
                "(Regira.Entities.EFcore.Extensions) — after your own HasQueryFilter(...) calls, exactly once. " +
                "If archived rows are meant to stay visible, say so explicitly with " +
                "UseEntities(o => o.DefaultArchivedFilter = ArchivedFilter.Included). " +
                "See entities.patterns → Soft Delete.");
        }
#endif
    }

#if NET10_0_OR_GREATER

    /// <summary>
    /// Names of the <see cref="IArchivable"/> entity types in <paramref name="contextType"/>'s model that carry
    /// no <see cref="ModelBuilderExtensions.ArchivedQueryFilterName"/> filter. Selects through the shared
    /// <see cref="ModelBuilderExtensions.IsArchivedFilterTarget"/>, so it cannot drift from what actually
    /// installs the filter — a derived type (filtered through its root) and an owned type (configured through
    /// its owner) would otherwise be reported as unprotected.
    /// </summary>
    private static (string[] Unfiltered, string? Error) UnfilteredArchivables(IServiceProvider provider, Type contextType)
    {
        try
        {
            // Building a model needs no connection; resolving a context still can throw (a missing provider,
            // a throwing factory) — a diagnostic must never take the host down for its own inspection.
            var dbContext = (DbContext)provider.GetRequiredService(contextType);
            var unfiltered = dbContext.Model.GetEntityTypes()
                .Where(t => ModelBuilderExtensions.IsArchivedFilterTarget(t)
                            && t.FindDeclaredQueryFilter(ModelBuilderExtensions.ArchivedQueryFilterName) == null)
                .Select(t => t.ClrType.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray();
            return (unfiltered, null);
        }
        catch (Exception ex)
        {
            return ([], ex.Message);
        }
    }

    /// <summary>The configured app-wide default, or <see cref="ArchivedFilter.Excluded"/> when unavailable.</summary>
    private static ArchivedFilter ResolveDefaultArchivedFilter(EntityValidationContext context)
    {
        try
        {
            return context.Provider.GetService<EntityQueryOptions>()?.DefaultArchivedFilter ?? ArchivedFilter.Excluded;
        }
        catch
        {
            return ArchivedFilter.Excluded;
        }
    }
#endif
}
