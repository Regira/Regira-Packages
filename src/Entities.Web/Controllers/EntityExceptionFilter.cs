using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// Gives every MVC action the entity write pipeline's status mapping: <see cref="EntityInputException"/> →
/// <b>400</b> with its <c>InputErrors</c> as ModelState, <see cref="EntityConstraintException"/> → <b>409</b>
/// with <see cref="EntityConstraintProblem"/>. Registered application-wide by
/// <c>ConfigureDefaultJsonOptions()</c> (see <c>MapEntityExceptions()</c>).
/// <para>
/// Without it the mapping reaches only <see cref="ControllerExtensions"/>' <c>Save</c>/<c>Delete</c> helpers,
/// leaving a hand-written domain action on an <c>EntityControllerBase</c> — <c>POST {id}/approve</c> and its
/// kind, which write through <c>IEntityService</c> and therefore run the same preppers — to answer 500 with a
/// stack trace for a rule breach the generated <c>PUT</c> answers 400 for. Those helpers still catch first,
/// so this filter only ever sees what escapes them.
/// </para>
/// </summary>
public class EntityExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case EntityInputException input:
                foreach (var error in input.InputErrors)
                {
                    context.ModelState.AddModelError(error.Key, error.Value);
                }
                // A rule breach with no field-level detail still belongs at 400 — carry the message itself
                // rather than returning an empty ModelState the client cannot act on.
                if (input.InputErrors.Count == 0)
                {
                    context.ModelState.AddModelError(string.Empty, input.Message);
                }
                context.Result = new BadRequestObjectResult(context.ModelState);
                context.ExceptionHandled = true;
                break;
            case EntityConstraintException:
                context.Result = new ConflictObjectResult(EntityConstraintProblem.Create());
                context.ExceptionHandled = true;
                break;
        }
    }
}
