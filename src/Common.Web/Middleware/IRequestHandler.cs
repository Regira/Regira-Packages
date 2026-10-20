using Microsoft.AspNetCore.Http;

namespace Regira.Web.Middleware;

[Obsolete("A middleware needs no interface. Will be removed in v7.")]
public interface IRequestHandler
{
    Task InvokeAsync(HttpContext httpContext);
}