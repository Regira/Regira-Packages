using System.Net;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Office.Word.testing.Abstractions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Clients.DependencyInjection;
using Regira.Office.MimeTypes;
using Regira.Office.Models;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Gotenberg;
using Regira.Office.Word.Gotenberg.DependencyInjection;
using Regira.Office.Word.Gotenberg.Internal;
using Regira.Office.Word.Models;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Office.Word.testing;

/// <summary>
/// Everything about the Gotenberg backend that needs no server: the checks made before a request, the
/// request itself (against a stub handler), the upload format, the page setup written into the
/// document, and the DI registration.
/// </summary>
[TestFixture]
public class GotenbergUnitTests() : WordAssetsTestsBase("Gotenberg")
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 stub");

    // ---- checks made before any request ----

    [TestCase(FileFormat.Docx)]
    [TestCase(FileFormat.Doc)]
    [TestCase(FileFormat.Dotx)]
    [TestCase(FileFormat.Dot)]
    [TestCase(FileFormat.Docm)]
    [TestCase(FileFormat.Dotm)]
    [TestCase(FileFormat.Html)]
    [TestCase(FileFormat.Rtf)]
    [TestCase(FileFormat.Odt)]
    [TestCase(FileFormat.EPub)]
    public void Convert_To_Anything_But_Pdf_Is_Not_Supported(FileFormat format)
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(TemplateInput("template.docx"), format));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("PDF only"));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [TestCase(FileFormat.Png)]
    [TestCase(FileFormat.Jpeg)]
    public void Convert_To_Image_Points_At_ToImages(FileFormat format)
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(TemplateInput("template.docx"), format));

        Assert.That(ex!.Message, Does.Contain("ToImages"));
    }

    [Test]
    public void Template_Input_Without_Creator_Is_Rejected()
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };
        input.Headers = [new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") }];

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(input, FileFormat.Pdf));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.GlobalParameters)));
            Assert.That(ex.Message, Does.Contain(nameof(WordTemplateInput.Headers)));
            Assert.That(ex.Message, Does.Contain(nameof(IWordCreator)));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [Test]
    public void Settings_On_A_Non_OpenXml_Source_Are_Rejected()
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());
        var options = new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings() };

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(TemplateInput("template.odt"), options));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(".odt"));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [Test]
    public void ToImages_Without_A_PdfToImageService_Throws()
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => service.ToImages(TemplateInput("template.docx")));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(IPdfToImageService)));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    // ---- the request ----

    [Test]
    public async Task A_Finished_Document_Is_Uploaded_As_Is()
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var template = ReadAsset("template.docx");

        // a default-initialised input carries empty collections and default options: nothing to render
        using var output = await service.Convert(new WordTemplateInput { Template = template }, FileFormat.Pdf);

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/" + WordService.ConvertRoute));
            Assert.That(request.Uploads.Single().Name, Is.EqualTo("files"));
            Assert.That(request.Uploads.Single().FileName, Is.EqualTo("document.docx"));
            Assert.That(request.Uploads.Single().Bytes, Is.EqualTo(template.GetBytes()));
            Assert.That(output.GetBytes(), Is.EqualTo(PdfBytes));
            Assert.That(output.ContentType, Is.EqualTo(ContentTypes.PDF));
        });
    }

    [Test]
    public async Task A_Template_Input_Is_Rendered_By_The_Creator_First()
    {
        var rendered = ReadAsset("lorem_ipsum.docx").GetBytes()!;
        var creator = new StubCreator(rendered);
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), creator: creator);
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };

        using var _ = await service.Convert(input, FileFormat.Pdf);

        var upload = handler.Requests.Single().Uploads.Single();
        Assert.Multiple(() =>
        {
            Assert.That(creator.Inputs, Is.EqualTo(new[] { input }));
            Assert.That(upload.FileName, Is.EqualTo("document.docx"));
            Assert.That(upload.Bytes, Is.EqualTo(rendered));
        });
    }

    [Test]
    public async Task A_Finished_Document_Skips_The_Creator()
    {
        var creator = new StubCreator([]);
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), creator: creator);

        using var _ = await service.Convert(TemplateInput("template.odt"), FileFormat.Pdf);

        Assert.Multiple(() =>
        {
            // the creators have no ODT reader; a document that needs no rendering must not reach them
            Assert.That(creator.Inputs, Is.Empty);
            Assert.That(handler.Requests.Single().Uploads.Single().FileName, Is.EqualTo("document.odt"));
        });
    }

    [Test]
    public async Task Page_Settings_Are_Written_Before_Upload()
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 }
        };

        using var _ = await service.Convert(TemplateInput("template.docx"), options);

        var pageSize = PageSizes(handler.Requests.Single().Uploads.Single().Bytes).Single();
        Assert.That(pageSize, Is.EqualTo((16838u, 23811u)));
    }

    [Test]
    public async Task ToImages_Rasterises_The_Converted_Pdf()
    {
        var rasteriser = new StubPdfToImageService();
        var imageOptions = new PdfToImagesOptions { Format = Regira.Media.Drawing.Enums.ImageFormat.Png };
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), new GotenbergWordConfig { ImageOptions = imageOptions }, rasteriser);

        var images = (await service.ToImages(TemplateInput("template.docx"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(images, Has.Count.EqualTo(2));
            Assert.That(rasteriser.Pdfs.Single(), Is.EqualTo(PdfBytes));
            Assert.That(rasteriser.Options.Single(), Is.SameAs(imageOptions));
        });
    }

    [Test]
    public void A_Server_Error_Surfaces_Status_And_Body()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("context deadline exceeded")
        });
        var service = new WordService(handler.CreateClient());

        var ex = Assert.ThrowsAsync<HttpRequestException>(() => service.Convert(TemplateInput("template.docx"), FileFormat.Pdf));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(ex.Message, Does.Contain("503"));
            Assert.That(ex.Message, Does.Contain("context deadline exceeded"));
            Assert.That(ex.Message, Does.Contain("--api-timeout"));
        });
    }

    // ---- the upload format ----

    [TestCase("template.docx", "docx")]
    [TestCase("template.doc", "doc")]
    [TestCase("template.dot", "doc")]
    [TestCase("template.odt", "odt")]
    [TestCase("template.html", "html")]
    public void SourceFormat_Is_Read_From_The_Content(string filename, string expected)
    {
        var bytes = ReadAsset(filename).GetBytes()!;

        Assert.That(SourceFormat.Resolve(bytes.ToBinaryFile(), bytes), Is.EqualTo(expected));
    }

    [Test]
    public void SourceFormat_Recognises_Rtf()
    {
        var bytes = Encoding.ASCII.GetBytes(@"{\rtf1\ansi Hello}");

        Assert.That(SourceFormat.Sniff(bytes), Is.EqualTo("rtf"));
    }

    [Test]
    public void SourceFormat_Prefers_The_File_Name()
    {
        var bytes = ReadAsset("template.docx").GetBytes()!;
        var file = new BinaryFileItem { Bytes = bytes, Length = bytes.Length, FileName = "Letter.DOTX" };

        Assert.That(SourceFormat.Resolve(file, bytes), Is.EqualTo("dotx"));
    }

    [Test]
    public void SourceFormat_Ignores_A_File_Name_Outside_The_Route()
    {
        var bytes = ReadAsset("template.docx").GetBytes()!;
        var file = new BinaryFileItem { Bytes = bytes, Length = bytes.Length, FileName = "upload.tmp" };

        Assert.That(SourceFormat.Resolve(file, bytes), Is.EqualTo("docx"));
    }

    [Test]
    public void SourceFormat_Rejects_Unrecognised_Content()
    {
        var bytes = ReadAsset("sample1.jpg").GetBytes()!;

        var ex = Assert.Throws<NotSupportedException>(() => SourceFormat.Resolve(bytes.ToBinaryFile(), bytes));

        Assert.That(ex!.Message, Does.Contain(nameof(INamedFile)));
    }

    // ---- page setup ----

    [TestCase(PageSize.A3, 16838u, 23811u)]
    [TestCase(PageSize.A4, 11906u, 16838u)]
    [TestCase(PageSize.A5, 8391u, 11906u)]
    [TestCase(PageSize.A6, 5953u, 8391u)]
    public void PageSizes_Match_Word(PageSize size, uint width, uint height)
    {
        Assert.That(Regira.Office.Word.Gotenberg.Internal.PageSizes.Twips(size), Is.EqualTo((width, height)));
    }

    [Test]
    public void PageSetup_Resizes_The_Page()
    {
        var output = Apply("template.docx", new DocumentSettings { PageSize = PageSize.A3 });

        using var doc = Open(output);
        var pageSize = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageSize>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(pageSize.Width!.Value, Is.EqualTo(16838u));
            Assert.That(pageSize.Height!.Value, Is.EqualTo(23811u));
            Assert.That(pageSize.Orient!.Value, Is.EqualTo(W.PageOrientationValues.Portrait));
        });
    }

    [Test]
    public void PageSetup_Landscape_Swaps_Width_And_Height()
    {
        var output = Apply("template.docx", new DocumentSettings { PageOrientation = PageOrientation.Landscape });

        using var doc = Open(output);
        var pageSize = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageSize>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(pageSize.Width!.Value, Is.EqualTo(16838u));
            Assert.That(pageSize.Height!.Value, Is.EqualTo(11906u));
            Assert.That(pageSize.Orient!.Value, Is.EqualTo(W.PageOrientationValues.Landscape));
        });
    }

    [Test]
    public void PageSetup_Writes_Margins_In_Twips()
    {
        var output = Apply("template.docx", new DocumentSettings { Margins = 36f });

        using var doc = Open(output);
        var margin = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageMargin>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(margin.Top!.Value, Is.EqualTo(720));
            Assert.That(margin.Bottom!.Value, Is.EqualTo(720));
            Assert.That(margin.Left!.Value, Is.EqualTo(720u));
            Assert.That(margin.Right!.Value, Is.EqualTo(720u));
            // untouched by the settings
            Assert.That(margin.Header!.Value, Is.EqualTo(708u));
        });
    }

    [Test]
    public void PageSetup_Scales_Pictures_With_The_Text_Width()
    {
        var input = ReadAsset("template_image.docx").GetBytes()!;
        var before = PictureExtents(input);

        var output = Apply("template_image.docx", new DocumentSettings { PageSize = PageSize.A3 });

        // A4 → A3 with 1417 twip side margins: (16838 − 2834) / (11906 − 2834)
        var scale = (16838d - 2834) / (11906d - 2834);
        var after = PictureExtents(output);
        // the picture's wp:extent and its a:ext
        Assert.That(before, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].Cx, Is.EqualTo(before[i].Cx * scale).Within(1));
                Assert.That(after[i].Cy, Is.EqualTo(before[i].Cy * scale).Within(1));
            }
        });
    }

    [Test]
    public void PageSetup_Scales_Tables_Spanning_The_Text_Width()
    {
        var input = ReadAsset("template.docx").GetBytes()!;
        var before = GridColumns(input);

        var output = Apply("template.docx", new DocumentSettings { PageSize = PageSize.A3 });

        var scale = (16838d - 2834) / (11906d - 2834);
        var after = GridColumns(output);
        Assert.That(after, Is.EqualTo(before.Select(width => Math.Round(width * scale))));
    }

    [Test]
    public void PageSetup_Leaves_Content_Alone_When_AutoScale_Is_Off()
    {
        var input = ReadAsset("template.docx").GetBytes()!;
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 },
            AutoScalePictures = false,
            AutoScaleTables = false
        };

        var output = OpenXmlPageSetup.Apply(input, options);

        Assert.Multiple(() =>
        {
            Assert.That(GridColumns(output), Is.EqualTo(GridColumns(input)));
            Assert.That(PictureExtents(output), Is.EqualTo(PictureExtents(input)));
        });
    }

    [Test]
    public void PageSetup_Without_Settings_Returns_The_Source()
    {
        var input = ReadAsset("template.docx").GetBytes()!;

        var output = OpenXmlPageSetup.Apply(input, new ConversionOptions { OutputFormat = FileFormat.Pdf });

        Assert.That(output, Is.SameAs(input));
    }

    // ---- registration ----

    [Test]
    public async Task AddGotenbergWord_Registers_Both_Capabilities_Against_The_Server()
    {
        var handler = new StubHandler(_ => Pdf());
        var services = new ServiceCollection();
        services.AddGotenbergWord(o =>
        {
            o.BaseUrl = "http://gotenberg.test:3000/root";
            o.Username = "user";
            o.Password = "secret";
        });
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var converter = provider.GetRequiredService<IWordConverter>();
        using var _ = await converter.Convert(TemplateInput("template.docx"), FileFormat.Pdf);

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(converter, Is.InstanceOf<WordService>());
            Assert.That(provider.GetRequiredService<IWordToImagesService>(), Is.InstanceOf<WordService>());
            Assert.That(request.Uri.ToString(), Is.EqualTo("http://gotenberg.test:3000/root/" + WordService.ConvertRoute));
            Assert.That(request.Authorization, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"))));
        });
    }

    [TestCase(true, TestName = "AddGotenbergWord_Keeps_Its_Client_Apart_From_AddOfficeClients(registered first)")]
    [TestCase(false, TestName = "AddGotenbergWord_Keeps_Its_Client_Apart_From_AddOfficeClients(registered last)")]
    public async Task AddGotenbergWord_Keeps_Its_Client_Apart_From_AddOfficeClients(bool gotenbergFirst)
    {
        // Gotenberg for Word, the Regira Office API for the rest — its IPdfToImageService feeds ToImages
        var handler = new StubHandler(_ => Pdf());
        var services = new ServiceCollection();
        void AddGotenberg() => services.AddGotenbergWord(o =>
        {
            o.BaseUrl = "http://gotenberg.test:3000";
            o.Username = "user";
            o.Password = "secret";
        });
        void AddOffice() => services.AddOfficeClients(o => o.BaseUrl = "http://office.test");
        if (gotenbergFirst)
        {
            AddGotenberg();
            AddOffice();
        }
        else
        {
            AddOffice();
            AddGotenberg();
        }
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var gotenberg = provider.GetServices<IWordConverter>().OfType<WordService>().Single();
        using var _ = await gotenberg.Convert(TemplateInput("template.docx"), FileFormat.Pdf);
        // the client Office.Clients registers for the same interface
        var officeClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IWordConverter));

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Uri.Host, Is.EqualTo("gotenberg.test"));
            Assert.That(request.Authorization, Does.StartWith("Basic "));
            Assert.That(officeClient.BaseAddress, Is.EqualTo(new Uri("http://office.test/")));
            Assert.That(officeClient.DefaultRequestHeaders.Authorization, Is.Null, "Gotenberg's credentials must not reach the Regira Office API");
        });
    }

    [Test]
    public void AddGotenbergWord_Resolves_Without_The_Optional_Services()
    {
        var services = new ServiceCollection();
        services.AddGotenbergWord(o => o.BaseUrl = "http://gotenberg.test:3000");
        using var provider = services.BuildServiceProvider();

        var toImages = provider.GetRequiredService<IWordToImagesService>();

        // resolved with the constructor's defaults: no rasteriser was registered
        Assert.ThrowsAsync<InvalidOperationException>(() => toImages.ToImages(TemplateInput("template.docx")));
    }

    [Test]
    public async Task AddGotenbergWord_Takes_The_Optional_Services_From_The_Container()
    {
        var handler = new StubHandler(_ => Pdf());
        var rasteriser = new StubPdfToImageService();
        var creator = new StubCreator(ReadAsset("lorem_ipsum.docx").GetBytes()!);
        var services = new ServiceCollection();
        services.AddSingleton<IPdfToImageService>(rasteriser);
        services.AddSingleton<IWordCreator>(creator);
        services.AddGotenbergWord(o => o.BaseUrl = "http://gotenberg.test:3000");
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };
        var images = await provider.GetRequiredService<IWordToImagesService>().ToImages(input);

        Assert.Multiple(() =>
        {
            Assert.That(images.Count(), Is.EqualTo(2));
            Assert.That(creator.Inputs, Has.Count.EqualTo(1));
            Assert.That(rasteriser.Pdfs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AddGotenbergWord_Requires_A_BaseUrl()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddGotenbergWord(_ => { }));
    }


    private byte[] Apply(string filename, DocumentSettings settings)
        => OpenXmlPageSetup.Apply(ReadAsset(filename).GetBytes()!, new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = settings });

    private static WordprocessingDocument Open(byte[] bytes)
        => WordprocessingDocument.Open(new MemoryStream(bytes), false);

    private static List<(uint Width, uint Height)> PageSizes(byte[] docx)
    {
        using var doc = Open(docx);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageSize>()
            .Select(size => (size.Width!.Value, size.Height!.Value))
            .ToList();
    }

    private static List<(long Cx, long Cy)> PictureExtents(byte[] docx)
    {
        using var doc = Open(docx);
        var body = doc.MainDocumentPart!.Document!.Body!;
        return body.Descendants<Wp.Extent>().Select(e => (e.Cx!.Value, e.Cy!.Value))
            .Concat(body.Descendants<A.Extents>().Select(e => (e.Cx!.Value, e.Cy!.Value)))
            .ToList();
    }

    private static List<double> GridColumns(byte[] docx)
    {
        using var doc = Open(docx);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.GridColumn>()
            .Select(column => double.Parse(column.Width!.Value!, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
    }

    private static HttpResponseMessage Pdf()
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(PdfBytes) };

    private static void UseHandler(IServiceCollection services, HttpMessageHandler handler)
        => services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));


    private sealed record Upload(string? Name, string? FileName, byte[] Bytes);

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, List<Upload> Uploads);

    /// <summary>
    /// Records every request, including the multipart parts, and answers with <c>respond</c>. Without
    /// one, any request is unexpected.
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public HttpClient CreateClient() => new(this, false) { BaseAddress = new Uri("http://gotenberg.test/") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uploads = new List<Upload>();
            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (var part in multipart)
                {
                    var disposition = part.Headers.ContentDisposition;
                    uploads.Add(new Upload(
                        disposition?.Name?.Trim('"'),
                        disposition?.FileNameStar ?? disposition?.FileName?.Trim('"'),
                        await part.ReadAsByteArrayAsync(cancellationToken)));
                }
            }
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), uploads));

            return respond?.Invoke(request)
                   ?? throw new InvalidOperationException("No request was expected.");
        }
    }

    private sealed class StubCreator(byte[] output) : IWordCreator
    {
        public List<WordTemplateInput> Inputs { get; } = [];

        public Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
        {
            Inputs.Add(input);
            return Task.FromResult(output.ToMemoryFile(ContentTypes.DOCX));
        }
    }

    private sealed class StubPdfToImageService : IPdfToImageService
    {
        public List<byte[]> Pdfs { get; } = [];
        public List<PdfToImagesOptions?> Options { get; } = [];

        public Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default)
        {
            Pdfs.Add(pdf.GetBytes()!);
            Options.Add(options);
            IList<IImageFile> images = [new ImageFile { Bytes = [1] }, new ImageFile { Bytes = [2] }];
            return Task.FromResult(images);
        }
    }
}
