using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.EFcore.Conventions;

/// <summary>
/// Model-finalizing convention that applies <c>e =&gt; !e.IsArchived</c> as the named
/// <see cref="ModelBuilderExtensions.ArchivedQueryFilterName"/> query filter to every entity type implementing
/// <see cref="IArchivable"/> — the same thing <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> does,
/// applied from the context's options instead of from <c>OnModelCreating</c>. It is what lets soft delete work
/// without a line of Regira plumbing in the consumer's <c>DbContext</c>.
/// <para>
/// Running at model finalization puts it <em>after</em> everything <c>OnModelCreating</c> configured, which is
/// the ordering the named filter needs: an anonymous <c>HasQueryFilter(...)</c> the app configured itself is
/// re-registered under <see cref="ModelBuilderExtensions.ModelQueryFilterName"/> (EF Core 10 refuses to build a
/// model that mixes an anonymous filter with named ones). It keeps applying exactly as before and, being named,
/// stays out of reach of the archived opt-out — which suspends
/// <see cref="ModelBuilderExtensions.ArchivedQueryFilterName"/> and nothing else.
/// </para>
/// <para>
/// An entity type that already carries the archived filter is skipped, so a <c>DbContext</c> that calls
/// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> itself composes with the automatic wiring
/// rather than fighting it.
/// </para>
/// <para>
/// <b><c>net8.0</c> (EF Core 9): does nothing</b>, for the same reason
/// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> is a no-op there — EF Core 9 has no named query
/// filters, so the archived opt-ins could only be honoured with the untargeted <c>IgnoreQueryFilters()</c>,
/// which would also suspend the app's own row security on a path that writes. Archived rows are excluded by
/// <c>FilterArchivablesQueryBuilder</c> at the root of the query there instead.
/// </para>
/// </summary>
internal sealed class ArchivedQueryFilterConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
#if NET10_0_OR_GREATER
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (!ModelBuilderExtensions.IsArchivedFilterTarget(entityType))
            {
                continue;
            }
            // the DbContext applied it itself — leave its model exactly as configured
            if (entityType.FindDeclaredQueryFilter(ModelBuilderExtensions.ArchivedQueryFilterName) != null)
            {
                continue;
            }

            var anonymous = entityType.GetDeclaredQueryFilters().FirstOrDefault(f => f.IsAnonymous)?.Expression;
            if (anonymous != null)
            {
                entityType.SetQueryFilter((LambdaExpression?)null);
                entityType.SetQueryFilter(ModelBuilderExtensions.ModelQueryFilterName, anonymous);
            }
            entityType.SetQueryFilter(
                ModelBuilderExtensions.ArchivedQueryFilterName,
                ModelBuilderExtensions.BuildArchivedFilter(entityType.ClrType));
        }
#endif
    }
}

/// <summary>
/// Registers <see cref="ArchivedQueryFilterConvention"/> in the convention set. Appended, so it runs after the
/// provider's own model-finalizing conventions (EF's <c>QueryFilterRewritingConvention</c> among them) have had
/// the app's filters.
/// </summary>
internal sealed class ArchivedQueryFilterConventionSetPlugin : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.ModelFinalizingConventions.Add(new ArchivedQueryFilterConvention());
        return conventionSet;
    }
}

/// <summary>
/// <see cref="DbContextOptionsBuilder"/> extension carrying <see cref="ArchivedQueryFilterConventionSetPlugin"/>,
/// so the archived query filter can be wired from <c>AddDbContext</c> (like the interceptors and the UTC
/// convention) instead of from the <c>DbContext</c>. <c>UseEntities&lt;TContext&gt;(o =&gt; o.UseDefaults())</c>
/// adds it automatically; see <c>DbContextOptionsBuilderExtensions.AddArchivedQueryFilter</c> for the
/// standalone call.
/// <para>
/// Internal plumbing: <c>AddArchivedQueryFilter()</c> is the whole public surface. Never constructed on
/// <c>net8.0</c> — the extension method returns without adding it there, so a net8 context's options are
/// exactly what they were before this existed.
/// </para>
/// </summary>
internal sealed class ArchivedQueryFilterOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Singleton<IConventionSetPlugin, ArchivedQueryFilterConventionSetPlugin>());

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => "using archived query filter ";
        public override int GetServiceProviderHashCode() => 0; // stateless — all instances are equivalent
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Regira.Entities.EFcore:ArchivedQueryFilter"] = "1";
    }
}
