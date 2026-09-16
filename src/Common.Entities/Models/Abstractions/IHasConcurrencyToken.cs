namespace Regira.Entities.Models.Abstractions;

/// <summary>
/// Opts an entity into optimistic concurrency with a token that works on every provider, SQLite included.<br />
/// <c>UseEntities&lt;TContext&gt;(o =&gt; o.UseDefaults())</c> declares <see cref="ConcurrencyToken"/> an EF Core
/// concurrency token — no <c>DbContext</c> change — and registers <c>HasConcurrencyTokenDbPrimer</c>, which mints a
/// new value on every insert and update. The write path compares the value the client sends back with the row's, so
/// a write built on a stale read answers 409. Carry it on both the read and the input DTO, without an initializer.
/// </summary>
public interface IHasConcurrencyToken
{
    /// <summary>
    /// Minted by <c>HasConcurrencyTokenDbPrimer</c> on insert (when empty) and on every update.
    /// <see cref="Guid.Empty"/> from a client means "not sent": that write is not compared with the row.
    /// </summary>
    public Guid ConcurrencyToken { get; set; }
}
