namespace Regira.Entities.Validation.FluentValidation;

/// <summary>
/// The rule sets <see cref="FluentEntityValidator"/> runs per write. <see cref="Add"/> and <see cref="Modify"/> run beside
/// the rules outside any rule set; <see cref="Remove"/> runs alone, so shape rules and their lookups stay off a delete.
/// </summary>
public static class EntityRuleSets
{
    /// <summary>Rules for an insert only.</summary>
    public const string Add = "Add";
    /// <summary>Rules for an update only.</summary>
    public const string Modify = "Modify";
    /// <summary>The only rules a delete runs.</summary>
    public const string Remove = "Remove";
}
