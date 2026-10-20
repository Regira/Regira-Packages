using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// Gives every MVC action the entity write pipeline's status mapping: <see cref="EntityInputException"/> →
/// <b>400</b> with its errors as a <see cref="ValidationProblemDetails"/>, <see cref="EntityConstraintException"/> → <b>409</b>
/// with <see cref="EntityConstraintProblem"/>, <see cref="EntityConcurrencyException"/> → <b>409</b> with
/// <see cref="EntityConcurrencyProblem"/>. Registered application-wide by
/// <c>ConfigureDefaultJsonOptions()</c> (see <c>MapEntityExceptions()</c>).
/// <para>
/// The entity controller bases carry the same mapping as <see cref="EntityConstraintConflictAttribute"/>, so their
/// actions, hand-written ones included, answer the same without it. It is what maps a <see cref="ControllerExtensions"/>
/// helper called from any other controller.
/// </para>
/// </summary>
public class EntityExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context) => EntityExceptionMapping.Handle(context);
}
