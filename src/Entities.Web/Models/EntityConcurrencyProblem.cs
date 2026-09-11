using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Models;

namespace Regira.Entities.Web;

/// <summary>
/// The single source of the 409 response body for <see cref="EntityConcurrencyException"/> — the MVC helpers,
/// the <c>[EntityConstraintConflict]</c> attribute and the application-wide
/// <see cref="Controllers.EntityExceptionFilter"/> all emit this shape.
/// Its title is what sets it apart from <see cref="EntityConstraintProblem"/>: both answer 409, but this one tells
/// the client to reload and try again, that one that the input itself was rejected.
/// </summary>
public static class EntityConcurrencyProblem
{
    public static ProblemDetails Create() => new()
    {
        Title = "Concurrency conflict",
        Detail = EntityConcurrencyException.ClientMessage,
        Status = StatusCodes.Status409Conflict,
    };
}
