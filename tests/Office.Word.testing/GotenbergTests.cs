using Docnet.Core;
using Docnet.Core.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Office.Word.testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.Models;
using Regira.Office.PDF.DocNET;
using Regira.Office.Word.Gotenberg;
using Regira.Office.Word.Models;

namespace Office.Word.testing;

/// <summary>
/// Shared scenarios come from <see cref="WordTestsBase"/>; Gotenberg converts to PDF and renders pages,
/// so only those scenarios are declared, followed by what this backend adds over the others.
/// </summary>
/// <remarks>
/// Runs against a real Gotenberg server. <c>GOTENBERG_URL</c> points at one that is already running;
/// otherwise the fixture starts a container, gated like the other container-backed suites
/// (<c>REGIRA_PROVIDER_TESTS=containers</c>), and skips rather than fails when Docker is unavailable.
/// </remarks>
[TestFixture]
[Category("Containers")]
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
    private readonly PdfManager _pdf = new(new ImageService());
    private IContainer? _container;

    public GotenbergTests() : this(new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
    {
    }

    // The server's address is only known once the container runs; HttpClient accepts a BaseAddress
    // up to its first request, so the service is built now and pointed at the server in OneTimeSetUp.
    private GotenbergTests(HttpClient http)
        : base(new WordService(http, pdfToImages: new PdfManager(new ImageService()), creator: new Regira.Office.Word.Mini.WordService()), "Gotenberg")
    {
        _http = http;
    }

    private WordService Gotenberg => (WordService)Backend;


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


    [TestCase(FileFormat.Pdf, "converted.pdf")]
    public override Task Convert_To(FileFormat format, string outputName) => base.Convert_To(format, outputName);

    [Test]
    public override Task From_A3_To_Pdf() => base.From_A3_To_Pdf();

    [Test]
    public override Task From_A4_To_Pdf_A3() => base.From_A4_To_Pdf_A3();

    [Test]
    public override Task To_Images() => base.To_Images();


    [TestCase("template.odt")]
    [TestCase("template.doc")]
    [TestCase("template.dot")]
    [TestCase("template.html")]
    public async Task Converts_Other_Word_Processing_Formats(string filename)
    {
        using var output = await Gotenberg.Convert(TemplateInput(filename), FileFormat.Pdf);
        await output.SaveAs(OutputPath($"from_{Path.GetExtension(filename).TrimStart('.')}.pdf"));

        Assert.That(System.Text.Encoding.ASCII.GetString(output.GetBytes()!, 0, 5), Is.EqualTo("%PDF-"));
    }

    [Test]
    public async Task Convert_Tags_Pdf_ContentType()
    {
        using var output = await Gotenberg.Convert(TemplateInput("template.docx"), FileFormat.Pdf);

        Assert.That(output.ContentType, Is.EqualTo(ContentTypes.PDF));
    }

    [Test]
    public async Task ToImages_Returns_One_Image_Per_Page()
    {
        using var pdf = await Gotenberg.Convert(TemplateInput("multipage.docx"), FileFormat.Pdf);
        var pageCount = await _pdf.GetPageCount(pdf);

        var images = (await Gotenberg.ToImages(TemplateInput("multipage.docx"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(pageCount, Is.GreaterThan(1));
            Assert.That(images, Has.Count.EqualTo(pageCount));
        });
        images.ForEach(image => image.Dispose());
    }

    [Test]
    public async Task A3_Setting_Renders_A3_Pages()
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 }
        };

        using var pdf = await Gotenberg.Convert(TemplateInput("template.docx"), options);
        var (width, height) = FirstPageSize(pdf.GetBytes()!);

        // A3 is 297 × 420 mm: 841.9 × 1190.6 pt
        Assert.Multiple(() =>
        {
            Assert.That(width, Is.EqualTo(842).Within(2));
            Assert.That(height, Is.EqualTo(1191).Within(2));
        });
    }

    [Test]
    public async Task Landscape_Setting_Renders_Landscape_Pages()
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageOrientation = PageOrientation.Landscape }
        };

        using var pdf = await Gotenberg.Convert(TemplateInput("template.docx"), options);
        await pdf.SaveAs(OutputPath("landscape.pdf"));
        var (width, height) = FirstPageSize(pdf.GetBytes()!);

        Assert.That(width, Is.GreaterThan(height));
    }

    [Test]
    public async Task Template_Input_Is_Rendered_By_The_Creator()
    {
        const string title = "Rendered before conversion";
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = title };

        using var pdf = await Gotenberg.Convert(input, FileFormat.Pdf);
        var text = await _pdf.GetText(pdf);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain(title));
            Assert.That(text, Does.Not.Contain("{{ title }}"));
        });
    }


    /// <summary>
    /// The first page's size in points: rendered at scale 1, a PDF page is as many pixels as it is points.
    /// </summary>
    private static (int Width, int Height) FirstPageSize(byte[] pdf)
    {
        using var reader = DocLib.Instance.GetDocReader(pdf, new PageDimensions(1d));
        using var page = reader.GetPageReader(0);
        return (page.GetPageWidth(), page.GetPageHeight());
    }
}
