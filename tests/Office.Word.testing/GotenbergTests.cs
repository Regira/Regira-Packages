using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Office.Word.testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.PDF.PdfPig;
using Regira.Office.Word.Gotenberg;
using Regira.Office.Word.Models;

namespace Office.Word.testing;

/// <summary>
/// Word.Gotenberg converts to PDF and renders pages, so it runs the shared conversion and page-image scenarios in
/// <see cref="WordTestsBase"/>; below is what it adds over the other backends.
/// </summary>
/// <remarks>
/// Runs against a real Gotenberg server. <c>GOTENBERG_URL</c> points at one that is already running;
/// otherwise the fixture starts a container, gated like the other container-backed suites
/// (<c>REGIRA_PROVIDER_TESTS=containers</c>), and skips rather than fails when Docker is unavailable.
/// </remarks>
[WordFixture]
[Category("Containers")]
[LeavesOut(WordFeature.Creating | WordFeature.Merging | WordFeature.TextExtraction | WordFeature.ImageExtraction | WordFeature.DocumentBuilder,
    "Gotenberg converts and renders pages only; a template is rendered by the creator it is given")]
[LeavesOut(WordFeature.OtherFormats, "Gotenberg converts to PDF only")]
[LeavesOut(WordFeature.NestedDocuments | WordFeature.HeadersAndFooters | WordFeature.InputOptions | WordFeature.TitledTables | WordFeature.AltTextPictures | WordFeature.Bookmarks | WordFeature.HtmlParameters,
    "its creator here is Word.Mini, which has none of them")]
public class GotenbergTests : WordTestsBase
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
    private GotenbergTests(HttpClient http)
        : base(new WordService(http, pdfToImages: new PdfService(new ImageService()), creator: new Regira.Office.Word.Mini.WordService()), "Gotenberg")
    {
        _http = http;
    }

    private WordService Gotenberg => (WordService)Backend;

    public static IEnumerable<TestCaseData> OutputFormats => [Output(FileFormat.Pdf, "application/pdf")];


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


    [TestCase("template.odt")]
    [TestCase("template.doc")]
    [TestCase("template.dot")]
    [TestCase("template.html")]
    public async Task Converts_Other_Word_Processing_Formats(string filename)
    {
        using var output = await Gotenberg.Convert(TemplateInput(filename), FileFormat.Pdf);
        var pdf = await ReadPdf(output);

        Assert.Multiple(() =>
        {
            Assert.That(Facts.Sniff(output.GetBytes()!), Is.EqualTo(FileFormat.Pdf));
            Assert.That(pdf.Pages, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task Template_Input_Is_Rendered_By_The_Creator()
    {
        const string title = "Rendered before conversion";
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = title };

        using var output = await Gotenberg.Convert(input, FileFormat.Pdf);
        var text = string.Join("\n", (await ReadPdf(output)).PageTexts);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain(title));
            Assert.That(text, Does.Not.Contain("{{ title }}"));
        });
    }
}
