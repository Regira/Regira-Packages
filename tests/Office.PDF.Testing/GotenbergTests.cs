using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.Gotenberg;
using Regira.Office.PDF.Models;
using Regira.Utilities;

namespace Office.PDF.Testing;

/// <summary>
/// PDF.Gotenberg runs every shared scenario in <see cref="HtmlToPdfTestsBase"/>; below is what it adds over the other
/// HTML backends.
/// </summary>
/// <remarks>
/// Runs against a real Gotenberg server. <c>GOTENBERG_URL</c> points at one that is already running;
/// otherwise the fixture starts a container, gated like the other container-backed suites
/// (<c>REGIRA_PROVIDER_TESTS=containers</c>), and skips rather than fails when Docker is unavailable.
/// </remarks>
[TestFixture]
[Category("Containers")]
public class GotenbergTests : HtmlToPdfTestsBase
{
    public const string UrlVariable = "GOTENBERG_URL";
    private const string ContainersVariable = "REGIRA_PROVIDER_TESTS";
    private const string ContainersValue = "containers";
    private const string Image = "gotenberg/gotenberg:8";
    private const int Port = 3000;

    // See CONTRIBUTING.md: keeps the container alive between runs while iterating.
    private static bool ReuseContainers => Environment.GetEnvironmentVariable("REGIRA_CONTAINER_REUSE") == "1";

    private readonly HttpClient _http;
    private IContainer? _container;

    public GotenbergTests() : this(new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
    {
    }

    // The server's address is only known once the container runs; HttpClient accepts a BaseAddress
    // up to its first request, so the service is built now and pointed at the server in OneTimeSetUp.
    private GotenbergTests(HttpClient http) : base(new PdfService(http), "Gotenberg")
    {
        _http = http;
    }


    [OneTimeSetUp]
    public async Task StartServer()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        if (string.IsNullOrWhiteSpace(url))
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(ContainersVariable), ContainersValue, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore($"Skipped: set {ContainersVariable}={ContainersValue} to run against a Gotenberg container, or {UrlVariable} to use a running server.");
            }

            try
            {
                _container = new ContainerBuilder(Image)
                    .WithPortBinding(Port, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(Port).ForPath("/health")))
                    .WithReuse(ReuseContainers)
                    .Build();
                await _container.StartAsync();
            }
            catch (Exception ex)
            {
                // a container that started but never passed its wait strategy is still running
                if (_container != null)
                {
                    try
                    {
                        await _container.DisposeAsync();
                    }
                    catch (Exception)
                    {
                        // Docker itself may be what failed
                    }
                    _container = null;
                }
                Assert.Ignore($"Gotenberg container could not start (Docker unavailable?): {ex.Message}");
            }

            url = $"http://{_container!.Hostname}:{_container.GetMappedPublicPort(Port)}";
        }

        _http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
    }

    [OneTimeTearDown]
    public async Task StopServer()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
        _http.Dispose();
    }


    /// <summary>
    /// Gotenberg reads the HTML from a file, where Chromium guesses an encoding when the document declares none.
    /// </summary>
    [Test]
    public async Task Html_Without_A_Charset_Reads_As_Utf8()
    {
        const string text = "Café €5 Ωmega";
        var input = new HtmlInput
        {
            HtmlContent = $"<p>{text}</p>",
            HeaderHtmlContent = $"<span>{text}</span>"
        };

        using var file = await Backend.Create(input);
        var pdf = await ReadPdf(file);

        Assert.That(pdf.Pages[0].Text, Does.Contain("Café").And.Contain("€5").And.Contain("Ωmega"));
    }

    [Test]
    public async Task Server_Refusal_Surfaces_As_HttpRequestException()
    {
        // margins that leave no room for content: Chromium refuses the print settings
        var input = new HtmlInput { HtmlContent = "<p>Body</p>", Margins = DimensionsUtility.MmToPt(160f) };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => Backend.Create(input));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.Message, Does.Contain(PdfService.ConvertRoute).And.Contain("400").And.Contain("Chromium"));
    }
}
