using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Regira.Security.Authentication.Web.Extensions;

public static class ModelStateExtensions
{
    public static ModelStateDictionary AddIdentityErrors(this ModelStateDictionary modelState, IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            modelState.AddModelError(error.Code, error.Description);
        }

        return modelState;
    }
}