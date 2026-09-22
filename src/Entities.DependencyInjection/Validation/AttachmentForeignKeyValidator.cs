using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports an attachments owner whose attachment navigation is not mapped to the link entity's
/// <c>ObjectId</c>.
/// <para>
/// <c>ObjectId</c> is not a conventional foreign-key name, and the link entity has no navigation back to its
/// owner, so an owner collection left to EF's conventions gets a <em>shadow</em> key (<c>ProductId</c>) of its
/// own. The attachments pipeline writes <c>ObjectId</c> and leaves that key null: every link row is saved, and
/// every one is orphaned. The owner's <c>Attachments</c> then loads empty, <c>?hasAttachment=true</c> matches
/// nothing, and the <c>/{id}/attachments</c> sub-routes — which filter on <c>ObjectId</c> — keep listing the
/// files, which masks it. Nothing errors.
/// </para>
/// <para>
/// Inspects the model EF actually built, so it reports the mapping in force rather than the configuration
/// call that was meant to produce it.
/// </para>
/// </summary>
internal sealed class AttachmentForeignKeyValidator : IEntityRegistrationValidator
{
    private static readonly string ObjectIdName = nameof(IHasObjectId<int>.ObjectId);

    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        using var scope = context.Provider.CreateScope();
        foreach (var inspectType in ValidationContextTypes.Inspectable(context))
        {
            IModel model;
            try
            {
                model = ((DbContext)scope.ServiceProvider.GetRequiredService(inspectType)).Model;
            }
            catch
            {
                // The archived-filter validator already reports an uninspectable context; saying it twice
                // adds noise without adding information.
                continue;
            }

            foreach (var navigation in MisMappedNavigations(model))
            {
                var owner = navigation.DeclaringEntityType.ClrType.Name;
                var link = navigation.TargetEntityType.ClrType.Name;
                var key = string.Join(", ", navigation.ForeignKey.Properties.Select(p => p.IsShadowProperty() ? $"{p.Name} (shadow)" : p.Name));
                yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                    $"{owner}.{navigation.Name} maps to {link} through {key}, not {ObjectIdName} (in {inspectType.Name}). " +
                    $"The attachments pipeline writes {ObjectIdName}, so every {link} row is saved with that key null: " +
                    $"{owner}.{navigation.Name} loads empty and ?hasAttachment=true matches nothing, while the /{{id}}/attachments " +
                    "sub-routes (which filter on ObjectId) still list the files. Nothing errors. " +
                    $"ACTION: in OnModelCreating, modelBuilder.Entity<{owner}>().HasMany(x => x.{navigation.Name}).WithOne()" +
                    $".HasForeignKey(x => x.{ObjectIdName}).HasPrincipalKey(x => x.Id); — then recreate the database, since the " +
                    "existing link rows carry no owner key. See entities.instructions → Attachments.");
            }
        }
    }

    /// <summary>
    /// Collection navigations from an attachments owner to an <see cref="IEntityAttachment"/> link whose foreign
    /// key is anything other than the link's single <c>ObjectId</c> property.
    /// </summary>
    private static IEnumerable<INavigation> MisMappedNavigations(IModel model)
        => model.GetEntityTypes()
            .Where(t => !t.IsOwned() && IsAttachmentsOwner(t.ClrType))
            .SelectMany(t => t.GetDeclaredNavigations())
            .Where(n => n.IsCollection && typeof(IEntityAttachment).IsAssignableFrom(n.TargetEntityType.ClrType))
            .Where(n => n.ForeignKey.Properties.Count != 1
                        || n.ForeignKey.Properties[0].IsShadowProperty()
                        || n.ForeignKey.Properties[0].Name != ObjectIdName)
            .OrderBy(n => n.DeclaringEntityType.ClrType.Name);

    /// <summary>The typed interfaces do not extend the non-generic marker, so both shapes are probed.</summary>
    private static bool IsAttachmentsOwner(Type entityType)
        => typeof(IHasAttachments).IsAssignableFrom(entityType)
           || entityType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasAttachments<,,,,>));
}
