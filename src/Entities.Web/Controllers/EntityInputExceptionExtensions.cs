using Microsoft.AspNetCore.Mvc.ModelBinding;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>The one mapping of an <see cref="EntityInputException"/> to the 400 body, shared by every catch site.</summary>
internal static class EntityInputExceptionExtensions
{
    /// <summary>
    /// Adds the rejection to <paramref name="modelState"/>: every entry of <see cref="EntityInputException.Errors"/> —
    /// several messages per field each stay their own, and what was set through <see cref="EntityInputException.InputErrors"/>
    /// is among them — otherwise the exception's message under the empty key. A rule breach with no field-level detail
    /// still belongs at 400, and an empty ModelState is nothing a client can act on.
    /// </summary>
    public static ModelStateDictionary AddEntityInputErrors(this ModelStateDictionary modelState, EntityInputException exception)
    {
        if (exception.Errors is { Count: > 0 } errors)
        {
            foreach (var error in errors)
            {
                modelState.AddModelError(error.Key, error.Message);
            }
        }
        else
        {
            modelState.AddModelError(string.Empty, exception.Message);
        }
        return modelState;
    }
}
