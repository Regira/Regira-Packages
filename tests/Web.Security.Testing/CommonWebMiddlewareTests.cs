#pragma warning disable CS0618 // Common.Web's own middlewares are obsolete
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Regira.Web.ExceptionHandling;
using Regira.Web.ExceptionHandling.Abstractions;
using Regira.Web.Middleware;
using Shouldly;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// Common.Web's own exception and culture middlewares, which <c>UseExceptionHandler()</c> and
/// <c>UseRequestLocalization()</c> replace.
/// </summary>
public class CommonWebMiddlewareTests
{
    /// <summary>
    /// A response already on its way cannot become a 500: the handler sees the exception, which then goes on, so the
    /// server aborts the response rather than end it as a cut-off 200.
    /// </summary>
    [Fact]
    public async Task An_Exception_After_The_Response_Started_Reaches_The_Handler_And_Goes_On()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponse());
        var handler = new RecordingHandler();
        var failure = new InvalidOperationException("thrown after the first byte");
        var middleware = new GlobalExceptionHandlingMiddleware(_ => throw failure, handler);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        thrown.ShouldBeSameAs(failure);
        handler.Handled.ShouldBeSameAs(failure);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("not a culture!")]
    public async Task A_Culture_That_Does_Not_Exist_Is_Ignored(string culture)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = QueryString.Create("culture", culture);
        var reached = false;
        var middleware = new RequestCultureMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        reached.ShouldBeTrue();
    }

    /// <summary>The culture a request names is a writable instance, as <c>new CultureInfo(name)</c> gives one.</summary>
    [Fact]
    public async Task A_Requested_Culture_Can_Be_Adjusted_Downstream()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = QueryString.Create("culture", "nl-BE");
        var name = "";
        var middleware = new RequestCultureMiddleware(_ =>
        {
            name = System.Globalization.CultureInfo.CurrentCulture.Name;
            System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol = "EUR";
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        name.ShouldBe("nl-BE");
    }

    private sealed class RecordingHandler : IExceptionHandler
    {
        public Exception? Handled { get; private set; }
        public Task HandleException(HttpContext context, Exception ex)
        {
            Handled = ex;
            return Task.CompletedTask;
        }
    }

    /// <summary>A response whose headers are on their way, as a server refuses a new status for it.</summary>
    private sealed class StartedResponse : IHttpResponseFeature
    {
        public int StatusCode
        {
            get => StatusCodes.Status200OK;
            set => throw new InvalidOperationException("StatusCode cannot be set because the response has already started.");
        }
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
