using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Controllers;

/// <summary>
/// Maps what the write pipeline refuses on the controller or action it is applied to, as
/// <see cref="EntityExceptionFilter"/> does application-wide: an <see cref="EntityInputException"/> to a 400 with its
/// errors, an <see cref="EntityConstraintException"/> to a 409 with the generic
/// <see cref="EntityConstraintException.ClientMessage"/> (the provider's constraint message is logged by the write
/// service), an <see cref="EntityConcurrencyException"/> to a 409 with <see cref="EntityConcurrencyProblem"/>, an
/// <see cref="EntityForbiddenException"/> to a 403 and an <see cref="EntityNotFoundException"/> to a 404.
/// The entity and attachment controller bases carry it, so their actions, hand-written ones included, answer the same in
/// a host that registers no filter.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class EntityConstraintConflictAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context) => EntityExceptionMapping.Handle(context);
}
