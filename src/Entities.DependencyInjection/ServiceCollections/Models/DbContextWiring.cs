namespace Regira.Entities.DependencyInjection.ServiceCollections.Models;

/// <summary>
/// The DbContext plumbing that <c>UseEntities&lt;TContext&gt;()</c> wires into the context's options
/// automatically (via EF's <c>IDbContextOptionsConfiguration&lt;TContext&gt;</c>), so <c>AddDbContext</c>
/// only needs the provider. Select via <c>EntityServiceCollectionOptions.WireDbContext(...)</c>:
/// <c>UseDefaults()</c> enables <see cref="All"/>; consumers not using <c>UseDefaults()</c> can pick
/// the pieces that match their manual registrations.
/// </summary>
[Flags]
public enum DbContextWiring
{
    None = 0,
    /// <summary>Runs registered <c>IEntityPrimer</c>s during <c>SaveChanges</c> (timestamps, soft-delete, custom primers).</summary>
    PrimerInterceptors = 1 << 0,
    /// <summary>Runs registered entity normalizers during <c>SaveChanges</c> (<c>[Normalized]</c> fields).</summary>
    NormalizerInterceptors = 1 << 1,
    /// <summary>Truncates strings to their <c>[MaxLength]</c> before <c>SaveChanges</c>.</summary>
    AutoTruncateInterceptors = 1 << 2,
    /// <summary>Rounds <c>DateTime</c> values through the database as UTC (see <c>UtcDateTimeConverter</c>).</summary>
    UtcDateTimeConvention = 1 << 3,
    /// <summary>
    /// Installs the soft-delete query filter (<c>e =&gt; !e.IsArchived</c>) on every <c>IArchivable</c> entity
    /// type, so archived rows are hidden without any Regira call in the <c>DbContext</c>. Equivalent to
    /// <c>modelBuilder.SetArchivedQueryFilter()</c> in <c>OnModelCreating</c>, which stays available for a
    /// context built outside DI. <c>net8.0</c>: no filter exists to install (EF Core 9 has no named query
    /// filters) and the flag does nothing.
    /// </summary>
    ArchivedQueryFilter = 1 << 4,

    All = PrimerInterceptors | NormalizerInterceptors | AutoTruncateInterceptors | UtcDateTimeConvention | ArchivedQueryFilter
}
