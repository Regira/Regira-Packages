namespace Regira.Entities.Models;

/// <summary>Whether the web save endpoints re-fetch the entity after <c>SaveChanges()</c> for the response.</summary>
public enum RefetchAfterSave
{
    /// <summary>Always re-fetch via <c>Details(id)</c> (current default).</summary>
    Always = 0,
    /// <summary>Re-fetch only when an <c>IEntityProcessor</c> is registered for the entity; otherwise return the saved input.</summary>
    WhenProcessorsRegistered = 1,
    /// <summary>Never re-fetch; the response carries the saved input entity.</summary>
    Never = 2
}

/// <summary>
/// Runtime carrier for the read-path behavior configured at <c>UseEntities()</c>.
/// Registered as a singleton and resolved by the read services and web save endpoints.
/// </summary>
public class EntityReadOptions
{
    /// <summary>See <see cref="Models.RefetchAfterSave"/>. Default re-fetches (<see cref="Models.RefetchAfterSave.Always"/>).</summary>
    public RefetchAfterSave RefetchAfterSave { get; set; }
}

/// <summary>
/// Per-entity override of the global <see cref="EntityReadOptions"/>, configured via
/// <c>SetReadBehavior(...)</c> on the entity service builder. When registered, it fully replaces the
/// global options for that entity.
/// </summary>
/// <typeparam name="TEntity">The entity this override applies to.</typeparam>
public class EntityReadOptions<TEntity> : EntityReadOptions where TEntity : class;
