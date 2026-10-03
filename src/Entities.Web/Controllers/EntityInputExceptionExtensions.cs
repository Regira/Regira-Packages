using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>The one mapping of an <see cref="EntityInputException"/> to the 400 body, shared by every catch site.</summary>
internal static class EntityInputExceptionExtensions
{
    /// <summary>The <see cref="ProblemDetails"/> extension member that lists every error with its args.</summary>
    public const string ErrorDetailsMember = "errorDetails";

    /// <summary>
    /// The 400 of a rejection: the <see cref="ValidationProblemDetails"/> model binding answers with too — its
    /// <c>errors</c> mapping each key to its messages — plus <c>errorDetails</c>, every error in order as
    /// <c>{ key, message, args }</c>, for a client that pairs a message with a translation that fills in the args. The errors are
    /// <see cref="EntityInputException.Errors"/>, several messages of a field each their own entry, or else the exception's
    /// message under the empty key: a rule breach with no field-level detail still belongs at 400, and an empty map is
    /// nothing a client can act on. Built by the app's <see cref="ProblemDetailsFactory"/>, so it carries the same
    /// <c>traceId</c> and customization as every other problem the app returns.
    /// </summary>
    public static BadRequestObjectResult ToBadRequest(this EntityInputException exception, HttpContext httpContext)
    {
        IReadOnlyList<EntityInputError> errors = exception.Errors is { Count: > 0 }
            ? [.. exception.Errors]
            : [new EntityInputError(string.Empty, exception.Message)];

        var modelState = new ModelStateDictionary();
        foreach (var error in errors)
        {
            modelState.AddModelError(error.Key, error.Message);
        }

        var problem = httpContext.RequestServices?.GetService<ProblemDetailsFactory>()?
                          .CreateValidationProblemDetails(httpContext, modelState, StatusCodes.Status400BadRequest)
                      ?? new ValidationProblemDetails(modelState) { Status = StatusCodes.Status400BadRequest };
        problem.Extensions[ErrorDetailsMember] = errors.Select(ToDetail).ToArray();
        return new BadRequestObjectResult(problem);
    }

    // a dictionary rather than a type: its keys go out as written whatever naming policy the host serializes with
    private static Dictionary<string, object?> ToDetail(EntityInputError error)
    {
        var detail = new Dictionary<string, object?>
        {
            ["key"] = error.Key,
            ["message"] = error.Message
        };
        var args = error.Args?.Where(x => IsScalar(x.Value)).ToDictionary(x => x.Key, x => x.Value);
        if (args is { Count: > 0 })
        {
            detail["args"] = args;
        }
        return detail;
    }

    // what a translation can fill into its text; anything else — an entity, a collection — it could not, and serializing
    // it can fail on a cycle or send far more than the error is about
    private static bool IsScalar(object? value) => value switch
    {
        null or string or char or bool or decimal or Enum => true,
        DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid => true,
        _ => value.GetType().IsPrimitive
    };
}
