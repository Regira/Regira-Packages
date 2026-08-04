using Microsoft.AspNetCore.Http;

namespace Regira.Web.ExceptionHandling.Abstractions;

public interface IExceptionHandler
{
    Task HandleException(HttpContext context, Exception ex);
}