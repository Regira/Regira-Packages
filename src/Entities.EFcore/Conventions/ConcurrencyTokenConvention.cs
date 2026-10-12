using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Conventions;

/// <summary>
/// Model-finalizing convention that marks <see cref="IHasConcurrencyToken.ConcurrencyToken"/> as an EF Core
/// concurrency token on every entity type implementing <see cref="IHasConcurrencyToken"/> — what
/// <c>[ConcurrencyCheck]</c> or <c>.IsConcurrencyToken()</c> would do, applied from the context's options, so the
/// marker needs no line in the consumer's <c>DbContext</c>.
/// <para>
/// Set at convention configuration source: an explicit
/// <c>.Property(x =&gt; x.ConcurrencyToken).IsConcurrencyToken(false)</c> in <c>OnModelCreating</c> wins, which is
/// the opt-out for one entity type. Unlike the archived query filter it needs nothing EF Core 10 adds, so it applies
/// on both target frameworks. Wired through <see cref="ConventionOptionsExtension{TConvention}"/>.
/// </para>
/// </summary>
internal sealed class ConcurrencyTokenConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!typeof(IHasConcurrencyToken).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }
            // the property may be declared on a mapped base type that does not implement the marker; the hierarchy
            // shares it either way, so it is configured wherever it is declared
            entityType.FindProperty(nameof(IHasConcurrencyToken.ConcurrencyToken))?.Builder.IsConcurrencyToken(true);
        }
    }
}
