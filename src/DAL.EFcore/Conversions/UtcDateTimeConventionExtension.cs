using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Regira.DAL.EFcore.Conversions;

/// <summary>
/// Model-finalizing convention that applies <see cref="UtcDateTimeConverter"/> to every
/// <see cref="DateTime"/> (and nullable) property, including those of complex types (<c>ComplexProperty</c> value
/// objects, nested ones too). Runs at convention precedence, so any explicit per-property conversion configured in
/// <c>OnModelCreating</c> wins (= per-property opt-out).
/// </summary>
public class UtcDateTimeConvention : IModelFinalizingConvention
{
    private static readonly UtcDateTimeConverter Converter = new();

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            Apply(entityType);
        }
    }

    // A complex type's properties are not the entity type's, so a DateTime inside a value object was read back
    // Unspecified — served without the Z — while Created on the same row was fine.
    private static void Apply(IConventionTypeBase type)
    {
        foreach (var property in type.GetDeclaredProperties())
        {
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
            {
                property.Builder.HasConversion(Converter);
            }
        }
        foreach (var complexProperty in type.GetDeclaredComplexProperties())
        {
            Apply(complexProperty.ComplexType);
        }
    }
}

/// <summary>
/// Registers <see cref="UtcDateTimeConvention"/> in the convention set.
/// </summary>
public class UtcDateTimeConventionSetPlugin : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.ModelFinalizingConventions.Add(new UtcDateTimeConvention());
        return conventionSet;
    }
}

/// <summary>
/// <see cref="DbContextOptionsBuilder"/> extension carrying <see cref="UtcDateTimeConventionSetPlugin"/>,
/// so the UTC convention can be wired from <c>AddDbContext</c> (like the interceptors) instead of
/// overriding <c>ConfigureConventions</c> on the <c>DbContext</c>.
/// </summary>
public class UtcDateTimeOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Singleton<IConventionSetPlugin, UtcDateTimeConventionSetPlugin>());

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "using UTC DateTimes ";
        public override int GetServiceProviderHashCode() => 0; // stateless — all instances are equivalent
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Regira.DAL.EFcore:UtcDateTimeConvention"] = "1";
    }
}
