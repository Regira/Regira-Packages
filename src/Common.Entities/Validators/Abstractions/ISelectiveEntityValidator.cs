namespace Regira.Entities.Validators.Abstractions;

/// <summary>
/// An <see cref="IEntityValidator"/> whose scope is wider than what it checks: scoped to every entity (<c>IEntity</c>), say,
/// while it has rules for a few — as the FluentValidation adapter's validator is, for the entities with an
/// <c>AbstractValidator</c>. The write pipeline runs it only for the items it covers, so a write of an entity it has
/// nothing to check for skips the change detection a refusal needs; startup validation reports only the entities it
/// covers when their write path cannot run the validators.
/// </summary>
public interface ISelectiveEntityValidator : IEntityValidator
{
    /// <summary>Whether the validator has anything to check for an item of <paramref name="entityType"/>.</summary>
    bool Covers(Type entityType);
}
