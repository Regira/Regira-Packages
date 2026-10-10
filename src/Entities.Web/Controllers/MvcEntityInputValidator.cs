using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// Checks an input DTO with MVC's own model validation, as <c>TryValidateModel</c> does: the rules a request body is
/// judged by, implicit <c>[Required]</c> on non-nullable references and the app's validator providers included, so a
/// patch's merged input fails exactly where the same body sent to <c>PUT</c> would. Registered in place of the
/// DataAnnotations default wherever this package is present; without MVC in the container it falls back to that default.
/// </summary>
public class MvcEntityInputValidator(IServiceProvider services) : IEntityInputValidator
{
    public virtual IReadOnlyList<EntityInputError> Validate(object input)
    {
        var validator = services.GetService<IObjectModelValidator>();
        if (validator == null)
        {
            return DataAnnotationsEntityInputValidator.Instance.Validate(input);
        }

        var actionContext = new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());
        validator.Validate(actionContext, validationState: null, prefix: string.Empty, model: input);
        return actionContext.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new EntityInputError(entry.Key,
                string.IsNullOrEmpty(error.ErrorMessage) ? error.Exception?.Message ?? "The value is not valid." : error.ErrorMessage)))
            .ToList();
    }
}
