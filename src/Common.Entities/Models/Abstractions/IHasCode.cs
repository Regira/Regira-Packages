namespace Regira.Entities.Models.Abstractions;

/// <summary>
/// Marks an entity as having a short unique code field.
/// </summary>
/// <remarks>
/// <c>Code</c> is declared as <c>string?</c> as a contract convention — the interface itself does not
/// enforce a value. If your domain requires a non-null code, add <c>[Required]</c> to the implementing
/// property; the <c>string?</c> type declaration remains unchanged. Use the auto-truncate and normaliser
/// interceptors to handle whitespace and casing.
/// </remarks>
public interface IHasCode
{
    string? Code { get; set; }
}