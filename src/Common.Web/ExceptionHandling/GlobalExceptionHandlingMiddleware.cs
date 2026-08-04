using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Regira.Web.ExceptionHandling.Abstractions;
using Regira.Web.Middleware;

namespace Regira.Web.ExceptionHandling;

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
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await exceptionHandler.HandleException(httpContext, ex);
        }
    }
}
public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}