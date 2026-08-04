using System.Net;
using Microsoft.AspNetCore.Http;

namespace Regira.Web.Utilities;

public static class RequestUtility
{
    extension(HttpRequest request)
    {
        public string CurrentUrl()
        {
            return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }

        public Uri GetBaseUrl()
        {
            return request.GetAbsoluteUrl();
        }

        public Uri GetAbsoluteUrl(string? relativeUrl = null)
        {
            var baseUri = new Uri($"{request.Scheme}://{request.Host.Value}");
            return string.IsNullOrEmpty(relativeUrl)
                ? new Uri(baseUri.ToString())
                : new Uri(baseUri, relativeUrl);
        }

        public Uri? GetReferrer()
        {
            return request.GetTypedHeaders().Referer;
        }

        public IPAddress? GetIPAddress()
        {
            return request.HttpContext.Connection.RemoteIpAddress;
        }
    }
}