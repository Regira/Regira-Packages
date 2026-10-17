namespace Regira.Entities.Attributes;

/// <summary>
/// Declares a concurrency token a version stamp: a value the server sets on every write, which the client reads,
/// echoes and never edits. The write path compares a stamp with the value the client sent, so a write built on a
/// stale read answers 409, and startup validation checks that the DTOs carry it. Put it beside
/// <c>[ConcurrencyCheck]</c> on an application-owned token a primer sets; a token the database generates on update
/// (<c>[Timestamp]</c>) and <c>IHasConcurrencyToken.ConcurrencyToken</c> are stamps without it.
/// <para>
/// A <c>[ConcurrencyCheck]</c> token without it is a data column the client edits, whatever moves it: compared with
/// the stored row, so only a write racing the save is caught, and a stale client is not refused. Startup validation
/// warns about an undeclared token of a type a stamp takes: a <c>Guid</c>, an integer, <c>byte[]</c>,
/// <c>DateTime</c> or <c>DateTimeOffset</c>.
/// </para>
/// <para>
/// A stamp the client leaves out (<c>null</c>, empty, the CLR default) is not compared: the write goes through, and
/// only a write racing it is caught. <see cref="Required"/> refuses such a write instead. The attribute also serves
/// on the implementing property of <c>IHasConcurrencyToken</c>, which is a stamp already, for that purpose alone.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class VersionStampAttribute : Attribute
{
    /// <summary>
    /// Refuses an update whose client left the stamp out, with <c>EntityInputException</c> — HTTP 400, the property
    /// name as the field — instead of writing unchecked. For a client that must always prove what it read. A
    /// <c>PATCH</c> without the token in its body still passes: the merge base supplies the value read at
    /// <c>PATCH</c> time. An insert is never refused, and a delete carries no client token to require.
    /// </summary>
    public bool Required { get; set; }
}
