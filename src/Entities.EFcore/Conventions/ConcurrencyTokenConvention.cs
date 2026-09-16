using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
/// on both target frameworks.
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

/// <summary>Registers <see cref="ConcurrencyTokenConvention"/> in the convention set.</summary>
internal sealed class ConcurrencyTokenConventionSetPlugin : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.ModelFinalizingConventions.Add(new ConcurrencyTokenConvention());
        return conventionSet;
    }
}

/// <summary>
/// <see cref="DbContextOptionsBuilder"/> extension carrying <see cref="ConcurrencyTokenConventionSetPlugin"/>, so the
/// concurrency token can be declared from <c>AddDbContext</c> instead of from the <c>DbContext</c>.
/// <c>UseEntities&lt;TContext&gt;(o =&gt; o.UseDefaults())</c> adds it automatically; see
/// <c>DbContextOptionsBuilderExtensions.AddConcurrencyTokenConvention</c> for the standalone call. Internal plumbing:
/// that call is the whole public surface.
/// </summary>
internal sealed class ConcurrencyTokenOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Singleton<IConventionSetPlugin, ConcurrencyTokenConventionSetPlugin>());

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "using concurrency tokens ";
        public override int GetServiceProviderHashCode() => 0; // stateless — all instances are equivalent
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Regira.Entities.EFcore:ConcurrencyTokens"] = "1";
    }
}
