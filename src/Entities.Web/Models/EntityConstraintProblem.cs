using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Models;

namespace Regira.Entities.Web;

/// <summary>
/// The single source of the 409 response body for <see cref="EntityConstraintException"/> —
/// every write surface (MVC helpers/filter, minimal-API filter, FastEndpoints bases) emits this shape.
/// Lives with the web response contract, not with any one of its handlers.
/// </summary>
public static class EntityConstraintProblem
{
    public static ProblemDetails Create() => new()
    {
        Title = "Conflict",
        Detail = EntityConstraintException.ClientMessage,
        Status = StatusCodes.Status409Conflict,
    };
}
