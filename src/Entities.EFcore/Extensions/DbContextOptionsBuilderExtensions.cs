using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Regira.Entities.EFcore.Conventions;

namespace Regira.Entities.EFcore.Extensions;

public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Installs the soft-delete query filter from the options builder — the <c>AddDbContext</c> counterpart of
    /// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> (use one or the other; using both is safe).
    /// Every entity type implementing <see cref="Regira.Entities.Models.Abstractions.IArchivable"/> gets the
    /// named <c>e =&gt; !e.IsArchived</c> filter, applied at model finalization so it lands after everything
    /// <c>OnModelCreating</c> configured.
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(options =&gt; options
    ///     .UseSqlServer(connectionString)
    ///     .AddArchivedQueryFilter());
    /// </code>
    /// <para>
    /// <c>UseEntities&lt;TContext&gt;(o =&gt; o.UseDefaults())</c> already adds this to the context's options
    /// (<c>DbContextWiring.ArchivedQueryFilter</c>), so an app on the default wiring never calls it. Reach for
    /// it when a context is built outside that wiring — a hand-constructed
    /// <c>new AppDbContext(new DbContextOptionsBuilder&lt;AppDbContext&gt;()...Options)</c> in tests, a
    /// design-time factory, or a seeding tool — because such a context builds its model without ever consulting
    /// the service collection.
    /// </para>
    /// <para>
    /// <b><c>net8.0</c> (EF Core 9): does nothing at all</b>, matching
    /// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> — no filter can be installed there, so no
    /// options extension is added either and the context's options stay byte-for-byte what they were without
    /// this call. Archived rows are excluded by <c>FilterArchivablesQueryBuilder</c> at the root of the query
    /// there instead.
    /// </para>
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder AddArchivedQueryFilter(this DbContextOptionsBuilder optionsBuilder)
    {
#if NET10_0_OR_GREATER
        var extension = optionsBuilder.Options.FindExtension<ArchivedQueryFilterOptionsExtension>() ?? new ArchivedQueryFilterOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
#endif
        return optionsBuilder;
    }

    /// <summary>
    /// Typed overload of <see cref="AddArchivedQueryFilter(DbContextOptionsBuilder)"/>, so the call keeps a
    /// <see cref="DbContextOptionsBuilder{TContext}"/> chain typed and its <c>Options</c> stay assignable to a
    /// <c>DbContext(DbContextOptions&lt;TContext&gt;)</c> constructor:
    /// <code>
    /// new AppDbContext(new DbContextOptionsBuilder&lt;AppDbContext&gt;()
    ///     .UseSqlite(connection)
    ///     .AddArchivedQueryFilter()
    ///     .Options);
    /// </code>
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder<TContext> AddArchivedQueryFilter<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)AddArchivedQueryFilter((DbContextOptionsBuilder)optionsBuilder);
}
