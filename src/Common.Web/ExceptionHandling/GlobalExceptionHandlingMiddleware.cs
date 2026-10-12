using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Regira.Web.ExceptionHandling.Abstractions;
using Regira.Web.Middleware;

namespace Regira.Web.ExceptionHandling;

[Obsolete("Use AddProblemDetails() with UseExceptionHandler(). Will be removed in v7.")]
public class GlobalExceptionHandlingMiddleware(RequestDelegate next, IExceptionHandler exceptionHandler)
    : IRequestHandler
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next.Invoke(httpContext);
        }
        catch (Exception ex)
        {
            var started = httpContext.Response.HasStarted;
            if (!started)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
            await exceptionHandler.HandleException(httpContext, ex);
            if (started)
            {
                // a response already on its way cannot become a 500: the server aborts it rather than end it as a
                // cut-off 200 the client cannot tell from a complete one
                throw;
            }
        }
    }
}
[Obsolete("Use AddProblemDetails() with UseExceptionHandler(). Will be removed in v7.")]
public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}