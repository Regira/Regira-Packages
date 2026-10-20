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
/// <c>DbContext</c> moves the token as well, and a client that read the row before that write is refused on its
/// next one. The raw write itself is compared with the token its entity was loaded with, unless the service sets the
/// original value to a client's token.
/// </para>
/// </summary>
public class HasConcurrencyTokenDbPrimer : EntityPrimerBase<IHasConcurrencyToken>
{
    public override async Task PrepareAsync(IHasConcurrencyToken entity, EntityEntry entry, CancellationToken token = default)
    {
        var property = entry.Property(nameof(IHasConcurrencyToken.ConcurrencyToken));
        // a token marked modified is a claim — the write path marks every property of an update it tracks — and is
        // compared as it stands, an empty stored token included
        if (entry.State is EntityState.Modified or EntityState.Deleted && !property.IsModified)
        {
            await CompareUnclaimedWithStored(entry, property, token);
        }

        if (entry.State == EntityState.Modified || (entry.State == EntityState.Added && entity.ConcurrencyToken == Guid.Empty))
        {
            entity.ConcurrencyToken = Guid.NewGuid();
        }
    }

    /// <summary>
    /// Points an update or delete that makes no claim — its token empty and not marked modified — at the stored token,
    /// so it writes, archives or deletes the row instead of failing as a conflict.
    /// </summary>
    /// <remarks>
    /// A delete is commonly issued from a stub — <c>Remove(new Order { Id = id })</c>, and the framework's own
    /// related-collection handling does the same — which carries no token, and so does the soft delete the
    /// <see cref="ArchivablePrimer"/> makes of it, or an update of an attached stub. EF builds
    /// <c>... WHERE ConcurrencyToken = @original</c>, so the row is compared against <see cref="Guid.Empty"/>, matches
    /// nothing, and the write fails as a concurrency conflict every time it is retried: opting an entity into the marker
    /// would otherwise make such deletes impossible. An empty token is the absence of a claim rather than a claim of
    /// emptiness, so the write goes ahead unconditionally. A caller that *does* hold a token keeps its check: a
    /// non-empty value is compared, and so is an empty one the write path tracked as the row's.
    /// <para>
    /// One read per such row, so removing N stubs in a loop is N round trips — a caller deleting many rows by key
    /// loads them in one query instead. A row another writer removed meanwhile stays a conflict.
    /// </para>
    /// </remarks>
    private static async Task CompareUnclaimedWithStored(EntityEntry entry, PropertyEntry property, CancellationToken token)
    {
        if (!Equals(property.OriginalValue, Guid.Empty))
        {
            return;
        }

        // a row that is already gone stays a conflict: there is nothing to delete, and saying so is the honest answer
        var stored = await entry.GetDatabaseValuesAsync(token);
        if (stored != null)
        {
            property.OriginalValue = stored[property.Metadata];
        }
    }
}
