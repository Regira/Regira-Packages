using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// The entity write pipeline's status mapping for MVC, shared by <see cref="EntityExceptionFilter"/> and
/// <see cref="EntityConstraintConflictAttribute"/>: <see cref="EntityInputException"/> → <b>400</b> with its errors as a
/// <see cref="ValidationProblemDetails"/>, <see cref="EntityConstraintException"/> → <b>409</b> with
/// <see cref="EntityConstraintProblem"/>, <see cref="EntityConcurrencyException"/> → <b>409</b> with
/// <see cref="EntityConcurrencyProblem"/>, <see cref="EntityForbiddenException"/> → <b>403</b> and
/// <see cref="EntityNotFoundException"/> → <b>404</b>, each a problem whose detail is the exception's message. Any other
/// exception is left as it is.
/// </summary>
internal static class EntityExceptionMapping
{
    public static void Handle(ExceptionContext context)
    {
        IActionResult? result = context.Exception switch
        {
            EntityInputException input => input.ToBadRequest(context.HttpContext),
            EntityConstraintException => Conflict(context.HttpContext, EntityConstraintProblem.Create()),
            EntityConcurrencyException => Conflict(context.HttpContext, EntityConcurrencyProblem.Create()),
            EntityForbiddenException or EntityNotFoundException => Problem(context.HttpContext, RefusalProblem(context.Exception)!),
            _ => null
        };
        if (result != null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }

    // MVC writes a ProblemDetails an ObjectResult carries as it is, so the app's factory builds it — as it builds the 400 —
    // for the traceId that ties the 409 to the write service's warning, its type, and the app's CustomizeProblemDetails
    private static ConflictObjectResult Conflict(HttpContext httpContext, ProblemDetails problem)
    {
        var factory = httpContext.RequestServices?.GetService<ProblemDetailsFactory>();
        return new ConflictObjectResult(
            factory?.CreateProblemDetails(httpContext, problem.Status, problem.Title, detail: problem.Detail) ?? problem);
    }

    private static ObjectResult Problem(HttpContext httpContext, ProblemDetails problem)
    {
        var factory = httpContext.RequestServices?.GetService<ProblemDetailsFactory>();
        return new ObjectResult(factory?.CreateProblemDetails(httpContext, problem.Status, problem.Title, detail: problem.Detail) ?? problem)
        {
            StatusCode = problem.Status
        };
    }

    /// <summary>The 403 of <see cref="EntityForbiddenException"/> and the 404 of <see cref="EntityNotFoundException"/>.</summary>
    internal static ProblemDetails? RefusalProblem(Exception exception) => exception switch
    {
        EntityForbiddenException => new ProblemDetails
        {
            Title = "Forbidden",
            Detail = exception.Message,
            Status = StatusCodes.Status403Forbidden,
        },
        EntityNotFoundException => new ProblemDetails
        {
            Title = "Not Found",
            Detail = exception.Message,
            Status = StatusCodes.Status404NotFound,
        },
        _ => null
    };
}
