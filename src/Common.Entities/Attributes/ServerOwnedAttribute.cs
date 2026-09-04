namespace Regira.Entities.Attributes;

/// <summary>
/// Marks a scalar property (or foreign key) as owned by the server: on update its value is restored from the
/// stored row, so a PUT/PATCH that omits it cannot null it or overwrite it. Enforced on the
/// <c>IEntityService</c> write path by <c>AutoServerOwnedPrepper</c>, registered by <c>UseDefaults()</c>.
/// <para>
/// Protect-only — to also mint a value on create, use <c>e.ServerOwned(x =&gt; x.Code, mintOnCreate)</c>.
/// Navigations, collections and <c>IArchivable.IsArchived</c> cannot be server-owned; startup validation
/// reports such a declaration and the enforcing prepper skips it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ServerOwnedAttribute : Attribute;
