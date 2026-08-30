using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports an entity that carries a foreign key to one of its own children while the child's own foreign key
/// cascades the delete back. The two rows then reference each other and are deleted together, which EF Core
/// cannot order.
/// <para>
/// The shape breaks twice, and neither failure names the relationship. At migration time SQL Server refuses
/// the schema outright — two cascade paths between the same pair of tables, <c>Msg 1785</c> — unless the
/// reference is mapped <c>ClientSetNull</c>; SQLite does not enforce that, so an app that develops on SQLite
/// meets it only when it migrates. At run time every delete of an owner whose children are loaded (which is
/// what a <c>DELETE /{id}</c> reading through the entity service does) is refused with <c>a circular
/// dependency was detected in the data to be saved</c> — a 500 on an endpoint that can never succeed.
/// </para>
/// <para>
/// Reported as a warning rather than an error because the model alone cannot prove the reference ever points
/// at a child of that same owner: pointed at another owner's row it is a perfectly ordinary optional relation
/// and nothing ever cycles.
/// </para>
/// </summary>
internal sealed class DeleteCycleValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        foreach (var inspectType in ValidationContextTypes.Inspectable(context))
        {
            DbContext dbContext;
            try
            {
                // Building a model needs no connection; resolving a context still can throw (a missing
                // provider, a throwing factory) — a diagnostic must never take the host down for its own
                // inspection, and the archived-filter validator already reports an uninspectable context.
                dbContext = (DbContext)context.Provider.GetRequiredService(inspectType);
            }
            catch
            {
                continue;
            }

            foreach (var issue in Cycles(dbContext, inspectType))
            {
                yield return issue;
            }
        }
    }

    private static IEnumerable<EntityValidationIssue> Cycles(DbContext dbContext, Type inspectType)
    {
        // Keyed on the unordered pair: where both foreign keys cascade, the same cycle is reachable from
        // either end and would otherwise be reported twice.
        var reported = new HashSet<string>();
        foreach (var entityType in dbContext.Model.GetEntityTypes().Where(t => !t.IsOwned()))
        {
            foreach (var reference in entityType.GetForeignKeys())
            {
                var child = reference.PrincipalEntityType;
                // A self-reference is a different shape: EF orders rows within one table itself, and the
                // hierarchy guides cover what it cannot.
                if (child.IsOwned() || child.ClrType == entityType.ClrType)
                {
                    continue;
                }

                // The child's link back to its owner. Only a cascading one deletes the pair together, which
                // is what turns the mutual reference into a save EF has to order and cannot.
                var owning = child.GetForeignKeys().FirstOrDefault(fk =>
                    fk.PrincipalEntityType.ClrType == entityType.ClrType
                    && fk.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.ClientCascade);
                var pair = string.Join("↔", new[] { entityType.ClrType.FullName, child.ClrType.FullName }.OrderBy(n => n, StringComparer.Ordinal));
                if (owning == null || !reported.Add(pair))
                {
                    continue;
                }

                yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                    Message(inspectType, entityType, reference, child, owning));
            }
        }
    }

    private static string Message(Type inspectType, IEntityType owner, IForeignKey reference, IEntityType child, IForeignKey owning)
    {
        var ownerName = owner.ClrType.Name;
        var childName = child.ClrType.Name;
        var referenceProperty = $"{ownerName}.{string.Join("+", reference.Properties.Select(p => p.Name))}";
        var owningProperty = $"{childName}.{string.Join("+", owning.Properties.Select(p => p.Name))}";
        var breakable = reference.Properties.All(p => p.IsNullable);

        // A database-level cascade action on the reference is the second path SQL Server counts; the Client*
        // behaviours map to NO ACTION and leave the schema acceptable.
        var schema = reference.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull
            ? $"On SQL Server the migration is refused before any of that — Msg 1785, \"may cause cycles or multiple cascade paths\", " +
              $"two cascade paths between the same two tables. Map {referenceProperty} as " +
              $".OnDelete(DeleteBehavior.ClientSetNull) (NO ACTION in the database, EF nulls the tracked reference) to get past it. "
            : "";

        var remedy = breakable
            ? $"If the reference has to stay, drop it in a save of its own: override BOTH SaveChanges(bool) and " +
              $"SaveChangesAsync(bool, ...) on {inspectType.Name} and delegate to SaveChangesBreakingDeleteCycles(...) / " +
              $"SaveChangesBreakingDeleteCyclesAsync(...) (Regira.Entities.EFcore.Extensions) — overriding only the async one " +
              "leaves every synchronous caller broken. Saves without a cycle stay a single round trip."
            : $"{referenceProperty} is required, so nothing can drop the reference and the pair can never be deleted together: " +
              "make it optional, or move the marker onto the child as above.";

        return $"{ownerName} references its own child {childName} in {inspectType.Name} " +
               $"({referenceProperty} → {childName}, while {owningProperty} → {ownerName} cascades). " +
               $"Deleting a {ownerName} whose {childName} rows are loaded — which is what a DELETE through the entity service does — " +
               "is refused by EF Core with \"a circular dependency was detected in the data to be saved\", so the delete answers 500 " +
               $"for every {ownerName} that has one. Nothing inside SaveChanges can fix it: primers and preppers do run for deleted " +
               "entries, but EF orders the deletes from the ORIGINAL foreign-key values, so what they set is never read. " +
               schema +
               $"ACTION: prefer marking the child over pointing at it — a flag or a rank column on {childName} identifies " +
               "the same row with no foreign key and no cycle. " +
               remedy +
               " See entities.patterns → An entity that references one of its own children.";
    }
}
