using Microsoft.AspNetCore.Http;

namespace Regira.Web.Middleware;

public interface IRequestHandler
{
    Task InvokeAsync(HttpContext httpContext);
}