using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Regira.Entities.EFcore.Conventions;

/// <summary>
/// <see cref="DbContextOptionsBuilder"/> extension that appends <typeparamref name="TConvention"/> to the context's
/// model-finalizing conventions, after the provider's own, so a convention is wired from <c>AddDbContext</c> instead
/// of from the <c>DbContext</c>. Stateless: every instance for one convention is equivalent. Internal plumbing: the
/// <c>DbContextOptionsBuilderExtensions</c> calls are the whole public surface.
/// <para>
/// Each convention has a named type of its own, which <c>FindExtension</c> matches exactly: the published
/// <c>Regira.Entities.DependencyInjection</c> 6.5.0 looks up <see cref="ConcurrencyTokenOptionsExtension"/> by name.
/// </para>
/// </summary>
internal abstract class ConventionOptionsExtension<TConvention> : IDbContextOptionsExtension
    where TConvention : IModelFinalizingConvention, new()
{
    private DbContextOptionsExtensionInfo? _info;
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Singleton<IConventionSetPlugin, Plugin>());

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class Plugin : IConventionSetPlugin
    {
        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            conventionSet.ModelFinalizingConventions.Add(new TConvention());
            return conventionSet;
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => $"using {typeof(TConvention).Name} ";
        public override int GetServiceProviderHashCode() => 0; // stateless — all instances are equivalent
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo[$"Regira.Entities.EFcore:{typeof(TConvention).Name}"] = "1";
    }
}

/// <summary>Wires <see cref="ArchivedQueryFilterConvention"/>; added by <c>AddArchivedQueryFilter()</c> on <c>net10.0</c> only.</summary>
internal sealed class ArchivedQueryFilterOptionsExtension : ConventionOptionsExtension<ArchivedQueryFilterConvention>;

/// <summary>Wires <see cref="ConcurrencyTokenConvention"/>; added by <c>AddConcurrencyTokenConvention()</c>.</summary>
internal sealed class ConcurrencyTokenOptionsExtension : ConventionOptionsExtension<ConcurrencyTokenConvention>;
