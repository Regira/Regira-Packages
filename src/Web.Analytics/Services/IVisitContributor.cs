using Microsoft.AspNetCore.Http;
using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// In-request hook for data only the live <see cref="HttpContext"/> has. Exceptions are logged and
/// swallowed. For HttpContext-free enrichment prefer <see cref="IPageViewEnricher{TPageView}"/>.
/// </summary>
public interface IVisitContributor<in TPageView>
    where TPageView : IPageView
{
    /// <summary>Before the endpoint runs — the only chance to enable request-body buffering.</summary>
    void OnCapturing(HttpContext context, TPageView view)
    {
    }

    /// <summary>After the response, right before enqueue; buffered body and HttpContext.Items are available here.</summary>
    ValueTask OnCapturedAsync(HttpContext context, TPageView view) => ValueTask.CompletedTask;
}