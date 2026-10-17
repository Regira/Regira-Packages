using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// The entity write pipeline's status mapping for MVC, shared by <see cref="EntityExceptionFilter"/> and
/// <see cref="EntityConstraintConflictAttribute"/>: <see cref="EntityInputException"/> → <b>400</b> with its errors as a
/// <see cref="ValidationProblemDetails"/>, <see cref="EntityConstraintException"/> → <b>409</b> with
/// <see cref="EntityConstraintProblem"/>, <see cref="EntityConcurrencyException"/> → <b>409</b> with
/// <see cref="EntityConcurrencyProblem"/>. Any other exception is left as it is.
/// </summary>
internal static class EntityExceptionMapping
{
    public static void Handle(ExceptionContext context)
    {
        IActionResult? result = context.Exception switch
        {
            EntityInputException input => input.ToBadRequest(context.HttpContext),
            EntityConstraintException => new ConflictObjectResult(EntityConstraintProblem.Create()),
            EntityConcurrencyException => new ConflictObjectResult(EntityConcurrencyProblem.Create()),
            _ => null
        };
        if (result != null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}
