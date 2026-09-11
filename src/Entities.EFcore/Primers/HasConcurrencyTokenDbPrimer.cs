using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Moves <see cref="IHasConcurrencyToken.ConcurrencyToken"/> on every write: a new value on update — a soft delete
/// included, since <see cref="ArchivablePrimer"/> turns it into one first — and on insert when the entity carries
/// none, so seeded and imported values survive. An application-owned token that nothing re-mints never changes, and
/// two clients holding the same value would both pass the check.
/// <para>
/// A primer rather than a prepper on purpose: it runs on every save, so a domain service writing through the raw
/// <c>DbContext</c> moves the token as well, and a client still holding the old value is refused.
/// </para>
/// </summary>
public class HasConcurrencyTokenDbPrimer : EntityPrimerBase<IHasConcurrencyToken>
{
    public override Task PrepareAsync(IHasConcurrencyToken entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State == EntityState.Modified || (entry.State == EntityState.Added && entity.ConcurrencyToken == Guid.Empty))
        {
            entity.ConcurrencyToken = Guid.NewGuid();
        }

        return Task.CompletedTask;
    }
}
