using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Regira.Web.Middleware;

// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-7.0
[Obsolete("Use UseRequestLocalization(). Will be removed in v7.")]
public class RequestCultureMiddleware(RequestDelegate next) : IRequestHandler
{
    public async Task InvokeAsync(HttpContext context)
    {
        var cultureQuery = context.GetRouteValue("culture")?.ToString() ?? context.Request.Query["culture"];
        if (!string.IsNullOrWhiteSpace(cultureQuery) && FindCulture(cultureQuery) is { } culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        // Call the next delegate/middleware in the pipeline.
        await next(context);
    }

    // a culture the platform does not know leaves the request's as it is; one it knows is set as a writable copy, since
    // the cached instance GetCultureInfo returns refuses changes to its formats downstream
    private static CultureInfo? FindCulture(string name)
    {
        try
        {
            return new CultureInfo(CultureInfo.GetCultureInfo(name, predefinedOnly: true).Name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
[Obsolete("Use UseRequestLocalization(). Will be removed in v7.")]
public static class RequestCultureMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestCulture(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestCultureMiddleware>();
    }
}