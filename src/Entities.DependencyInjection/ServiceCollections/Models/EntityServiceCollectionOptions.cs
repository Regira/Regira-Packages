using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models;

namespace Regira.Entities.DependencyInjection.ServiceCollections.Models;

public class EntityServiceCollectionOptions(IServiceCollection services)
{
    public IServiceCollection Services => services;
    public Func<IServiceCollection, IEntityMapConfigurator>? EntityMapConfiguratorFactory { get; set; }
    /// <summary>
    /// Optional registrar that wires up an <see cref="Regira.Entities.Attachments.Abstractions.IAttachmentUriResolver{TEntityAttachment}"/>
    /// for attachment DTOs. Set via the web-side <c>UseAttachmentUris()</c> extension. When <c>null</c>, attachment
    /// DTOs get a <c>null</c> Uri (no ASP.NET Core dependency required).
    /// </summary>
    public IAttachmentUriResolverRegistrar? AttachmentUriRegistrar { get; set; }

    private int? _defaultPageSize;
    private int? _maxPageSize;

    /// <summary>
    /// Default page size applied by the List/Search endpoints when the caller omits paging.
    /// <c>null</c> = no default (an omitted request is bounded only by <see cref="MaxPageSize"/>).
    /// </summary>
    public int? DefaultPageSize
    {
        get => _defaultPageSize;
        set { _defaultPageSize = value; PageSizeConfigured = true; }
    }
    /// <summary>
    /// Maximum page size enforced by the List/Search endpoints. Any requested page size larger than this is clamped down.
    /// <c>null</c> means no upper limit.
    /// </summary>
    public int? MaxPageSize
    {
        get => _maxPageSize;
        set { _maxPageSize = value; PageSizeConfigured = true; }
    }
    /// <summary>
    /// Whether paging defaults were explicitly configured on this options instance (via <see cref="DefaultPageSize"/>,
    /// <see cref="MaxPageSize"/> or <see cref="SetPageSize"/>). Used so that across multiple <c>UseEntities()</c> calls
    /// the last explicit configuration wins and a call that never touches paging does not clobber an earlier one.
    /// </summary>
    public bool PageSizeConfigured { get; private set; }
    /// <summary>
    /// Sets the default and maximum page sizes for the List/Search endpoints. See <see cref="DefaultPageSize"/> and <see cref="MaxPageSize"/>.
    /// Calling it with no arguments (both <c>null</c>) opts out of paging: an omitted or <c>0</c> page size then returns every row.
    /// </summary>
    /// <param name="pageSize">The default page size (applied when the caller omits paging).</param>
    /// <param name="maxPageSize">The maximum page size — also the size a <c>pageSize &lt;= 0</c> opt-out falls back to.</param>
    /// <returns>The current <see cref="EntityServiceCollectionOptions"/> instance.</returns>
    public EntityServiceCollectionOptions SetPageSize(int? pageSize = null, int? maxPageSize = null)
    {
        DefaultPageSize = pageSize;
        MaxPageSize = maxPageSize;
        return this;
    }

    private ArchivedFilter _defaultArchivedFilter;

    /// <summary>
    /// Which rows of an <c>IArchivable</c> entity a query returns when the search object leaves
    /// <c>Archived</c> null. Default (<see cref="ArchivedFilter.Excluded"/>) hides archived rows.
    /// </summary>
    public ArchivedFilter DefaultArchivedFilter
    {
        get => _defaultArchivedFilter;
        set { _defaultArchivedFilter = value; QueryBehaviorConfigured = true; }
    }
    /// <summary>
    /// Whether query behavior was explicitly configured on this options instance. Mirrors
    /// <see cref="PageSizeConfigured"/>: across multiple <c>UseEntities()</c> calls the last explicit
    /// configuration wins and an untouched call never clobbers an earlier one.
    /// </summary>
    public bool QueryBehaviorConfigured { get; private set; }

    private RefetchAfterSave _refetchAfterSave;

    /// <summary>Whether the web save endpoints re-fetch the entity after <c>SaveChanges()</c>. Default re-fetches.</summary>
    public RefetchAfterSave RefetchAfterSave
    {
        get => _refetchAfterSave;
        set { _refetchAfterSave = value; ReadBehaviorConfigured = true; }
    }
    /// <summary>Whether read behavior was explicitly configured (same last-explicit-wins semantics as paging).</summary>
    public bool ReadBehaviorConfigured { get; private set; }

    /// <summary>Startup-validation settings; configure via <see cref="ConfigureValidation"/>.</summary>
    public EntityValidationOptions Validation { get; } = new();
    /// <summary>Whether <see cref="ConfigureValidation"/> was called (same last-explicit-wins semantics as paging).</summary>
    public bool ValidationConfigured { get; private set; }
    /// <summary>
    /// Configures the startup validation of entity registrations (arity mismatches, unwired
    /// interceptors, ignored search inputs). Runs in Development by default; set
    /// <c>v.Enabled = true</c> to opt in for Production or <c>false</c> to disable.
    /// </summary>
    public EntityServiceCollectionOptions ConfigureValidation(Action<EntityValidationOptions> configure)
    {
        configure(Validation);
        ValidationConfigured = true;
        return this;
    }

    /// <summary>
    /// The DbContext plumbing that <c>UseEntities&lt;TContext&gt;()</c> wires into the context's options
    /// automatically, so <c>AddDbContext</c> only needs the provider. <c>UseDefaults()</c> enables
    /// <see cref="Models.DbContextWiring.All"/>; without <c>UseDefaults()</c> pick the pieces matching your
    /// manual registrations via <see cref="WireDbContext"/>.<br />
    /// The standalone <c>AddAutoTruncateInterceptors()</c> / <c>AddUtcDateTimeConvention()</c> extensions
    /// (<c>Regira.DAL.EFcore</c>) compose safely — wiring is idempotent, so double configuration cannot occur.
    /// </summary>
    public DbContextWiring DbContextWiring { get; set; }

    /// <summary>
    /// Selects the DbContext plumbing that <c>UseEntities&lt;TContext&gt;()</c> wires into the context's
    /// options automatically (see <see cref="DbContextWiring"/>). Defaults to everything; combine flags for
    /// à-la-carte wiring, e.g. <c>WireDbContext(DbContextWiring.PrimerInterceptors)</c> for a setup that only
    /// registers primers, or <c>WireDbContext(DbContextWiring.None)</c> (after <c>UseDefaults()</c>) to wire
    /// the DbContext options yourself.
    /// </summary>
    /// <param name="wiring"></param>
    /// <returns>The current <see cref="EntityServiceCollectionOptions"/> instance.</returns>
    public EntityServiceCollectionOptions WireDbContext(DbContextWiring wiring = DbContextWiring.All)
    {
        DbContextWiring = wiring;
        return this;
    }

    /// <summary>
    /// Selects the default DbContext plumbing (<see cref="Models.DbContextWiring.All"/>): the
    /// primer/normalizer/auto-truncate SaveChanges interceptors, the UTC date convention and the archived
    /// query filter — shorthand for <see cref="WireDbContext"/> with <see cref="Models.DbContextWiring.All"/>.
    /// <c>UseEntities&lt;TContext&gt;()</c> wires the selection into the context's options automatically
    /// (assignability match — abstract-base registrations cover derived provider-specific contexts, in any
    /// registration order). Called by <c>UseDefaults()</c>; call it directly in setups that skip
    /// <c>UseDefaults()</c> but want the full default wiring.
    /// </summary>
    /// <returns>The current <see cref="EntityServiceCollectionOptions"/> instance.</returns>
    public EntityServiceCollectionOptions AddDefaultInterceptors()
        => WireDbContext();

    /// <summary>
    /// Enables (default) or disables UTC handling of entity <see cref="DateTime"/> values, process-wide
    /// (sets <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/>): timestamp primers write <see cref="DateTime.UtcNow"/>
    /// and normalize client-supplied values, query filters normalize input dates to UTC, and the UTC date
    /// convention's converter follows the same policy (it passes values through unchanged when UTC is disabled).<br />
    /// When disabled, timestamps use <see cref="DateTime.Now"/> and <see cref="DateTime"/> values are used exactly as given.
    /// </summary>
    /// <param name="enabled"></param>
    /// <returns>The current <see cref="EntityServiceCollectionOptions"/> instance.</returns>
    public EntityServiceCollectionOptions UseUtc(bool enabled = true)
    {
        Utilities.DateTimeDefaults.UseUtc = enabled;
        return this;
    }

    // See extension methods for implementations
}