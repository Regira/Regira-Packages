using Microsoft.AspNetCore.Http;
using Regira.Entities.Models;
using Regira.Entities.Web.Controllers;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// Gives a minimal-API endpoint the entity write pipeline's status mapping, with the bodies the controllers answer with:
/// <see cref="EntityInputException"/> → <b>400</b>, a <c>ValidationProblemDetails</c> with <c>errorDetails</c>;
/// <see cref="EntityConstraintException"/> → <b>409</b> with <see cref="EntityConstraintProblem"/>;
/// <see cref="EntityConcurrencyException"/> → <b>409</b> with <see cref="EntityConcurrencyProblem"/>.
/// <c>MapEntityEndpoints()</c> puts it on its group; an app's own endpoint that sends entity requests takes it with
/// <c>.AddEndpointFilter&lt;EntityExceptionEndpointFilter&gt;()</c>. The minimal-API counterpart of
/// <see cref="EntityExceptionFilter"/>.
/// </summary>
public sealed class EntityExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (EntityInputException ex)
        {
            return Results.Problem(ex.ToValidationProblem(context.HttpContext));
        }
        catch (EntityConstraintException)
        {
            return Results.Problem(EntityConstraintProblem.Create());
        }
        catch (EntityConcurrencyException)
        {
            return Results.Problem(EntityConcurrencyProblem.Create());
        }
    }
}
