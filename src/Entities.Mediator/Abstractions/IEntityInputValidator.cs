using Regira.Entities.Models;

namespace Regira.Entities.Mediator.Abstractions;

/// <summary>
/// Checks an input DTO's DataAnnotations before a save or a patch maps it — nested objects and collection items
/// included. A failure is thrown as an <c>EntityInputException</c>, so the 400 has the body of a validator's refusal.
/// <para>
/// The default validates with <c>System.ComponentModel.DataAnnotations</c>. Regira.Entities.Web replaces it in an MVC
/// host with MVC's own model validation, so a patch is judged exactly as a request body is.
/// </para>
/// </summary>
public interface IEntityInputValidator
{
    /// <summary>Every error of <paramref name="input"/>, keyed by property path (<c>Lines[0].Quantity</c>); empty when it is valid.</summary>
    IReadOnlyList<EntityInputError> Validate(object input);
}
