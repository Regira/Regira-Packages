using System.Net;
using Microsoft.AspNetCore.Http;

namespace Regira.Web.Utilities;

public static class HttpContextUtility
{
    public static Task WriteJsonError(this HttpContext context, string serializedError)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        return context.Response.WriteAsync(serializedError);
    }
}