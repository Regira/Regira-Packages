using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Regira.Web.Middleware;

// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-7.0
public class RequestCultureMiddleware(RequestDelegate next) : IRequestHandler
{
    public async Task InvokeAsync(HttpContext context)
    {
        var cultureQuery = context.GetRouteValue("culture")?.ToString() ?? context.Request.Query["culture"];
        if (!string.IsNullOrWhiteSpace(cultureQuery))
        {
            var culture = new CultureInfo(cultureQuery);

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        // Call the next delegate/middleware in the pipeline.
        await next(context);
    }
}
public static class RequestCultureMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestCulture(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestCultureMiddleware>();
    }
}