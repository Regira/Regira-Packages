using Microsoft.AspNetCore.Http;

namespace Regira.Web.ExceptionHandling.Abstractions;

[Obsolete("Use AddProblemDetails() with UseExceptionHandler(). Will be removed in v7.")]
public interface IExceptionHandler
{
    Task HandleException(HttpContext context, Exception ex);
}