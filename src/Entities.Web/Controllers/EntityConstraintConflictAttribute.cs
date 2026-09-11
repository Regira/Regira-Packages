using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// Maps the two conflicts the write pipeline raises to a 409 Conflict: an uncaught
/// <see cref="EntityConstraintException"/> with the generic <see cref="EntityConstraintException.ClientMessage"/>
/// (the provider's constraint message is logged by the write service), and an uncaught
/// <see cref="EntityConcurrencyException"/> with <see cref="EntityConcurrencyProblem"/>. Applied to the controller
/// bases whose write actions call <c>SaveChanges</c> directly instead of going through the
/// <see cref="ControllerExtensions"/> helpers.
/// <para>
/// Belt and braces: the application-wide <see cref="EntityExceptionFilter"/> that
/// <c>ConfigureDefaultJsonOptions()</c> registers already produces these responses for every action. The
/// attribute keeps those bases correct in a host that calls neither, and remains the way to scope the
/// mapping to a single controller or action.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class EntityConstraintConflictAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case EntityConstraintException:
                context.Result = new ConflictObjectResult(EntityConstraintProblem.Create());
                context.ExceptionHandled = true;
                break;
            case EntityConcurrencyException:
                context.Result = new ConflictObjectResult(EntityConcurrencyProblem.Create());
                context.ExceptionHandled = true;
                break;
        }
    }
}
