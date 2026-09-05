using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// Maps an uncaught <see cref="EntityConstraintException"/> to a 409 Conflict with the generic
/// <see cref="EntityConstraintException.ClientMessage"/> (the provider's constraint message is logged by
/// the write service). Applied to the controller bases whose write actions call
/// <c>SaveChanges</c> directly instead of going through the <see cref="ControllerExtensions"/> helpers.
/// <para>
/// Belt and braces: the application-wide <see cref="EntityExceptionFilter"/> that
/// <c>ConfigureDefaultJsonOptions()</c> registers already produces this response for every action. The
/// attribute keeps those bases correct in a host that calls neither, and remains the way to scope the
/// mapping to a single controller or action.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class EntityConstraintConflictAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is EntityConstraintException)
        {
            context.Result = new ConflictObjectResult(EntityConstraintProblem.Create());
            context.ExceptionHandled = true;
        }
    }
}
