using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Regira.IO.Models;
using Regira.Media.Drawing.Dimensions;
using Regira.Office.Clients.Services;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Office.Word.Models.DTO;

namespace Office.Clients.Testing;

/// <summary>
/// What <see cref="WordClient"/> puts on the wire, read back as the Office API reads it: the JSON body of
/// <c>word/create</c> through <c>DtoExtensions</c>, and the form fields <c>word/convert</c> binds its
/// <see cref="WordConversionModel"/> from — the API reads none of them from the query string. The answers are
/// canned, so no API is called.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Network")]
public class WordClientRequestTests
{
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Test]
    public async Task Create_Sends_Options_And_Picture_Layout()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        await new WordClient(http).Create(Contract());

        var request = handler.Requests.Single();
        Assert.That(request.PathAndQuery, Is.EqualTo("/word/create"));
        var received = JsonSerializer.Deserialize<WordDocumentInputDto>(request.Body!, ApiJson)!.ToWordTemplateInput();
        Assert.That(received.Options, Is.EqualTo(new InputOptions { RemoveEmptyParagraphs = true, EnforceEvenAmountOfPages = true }));
        var logo = received.Images!.Single();
        Assert.That(logo.Size, Is.EqualTo(new ImageSize(200, 80)));
        Assert.That(logo.HorizontalAlignment, Is.EqualTo(HorizontalAlignment.Center));
        Assert.That(received.DocumentParameters!["addendum"].Options, Is.EqualTo(new InputOptions { InheritFont = true }));
        Assert.That(received.Footers!.Single().Template.Options, Is.EqualTo(new InputOptions { HorizontalAlignment = HorizontalAlignment.Right }));
    }

    [Test]
    public async Task Convert_Sends_Its_Options_As_Form_Fields()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        await new WordClient(http).Convert(Contract(), new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 },
            AutoScaleTables = false
        });

        var convert = handler.Requests.Last();
        Assert.That(convert.PathAndQuery, Is.EqualTo("/word/convert"));
        Assert.That(convert.Fields, Is.EqualTo(new Dictionary<string, string>
        {
            [nameof(WordConversionModel.OutputFormat)] = "Pdf",
            [nameof(WordConversionModel.PageSize)] = "A3",
            [nameof(WordConversionModel.AutoScaleTables)] = "false",
            [nameof(WordConversionModel.AutoScalePictures)] = "true"
        }));
        Assert.That(convert.Files, Is.EqualTo(new[] { "file" }));
    }

    [Test]
    public async Task Convert_Without_Settings_Sends_No_Page_Size()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        await new WordClient(http).Convert(Contract(), FileFormat.Rtf);

        var convert = handler.Requests.Last();
        Assert.That(convert.Fields, Is.EqualTo(new Dictionary<string, string>
        {
            [nameof(WordConversionModel.OutputFormat)] = "Rtf",
            [nameof(WordConversionModel.AutoScaleTables)] = "true",
            [nameof(WordConversionModel.AutoScalePictures)] = "true"
        }));
    }

    /// <summary>The API's conversion takes no orientation or margins; asking for them fails before anything is sent.</summary>
    [Test]
    public async Task Convert_Rejects_Orientation_And_Margins()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };
        var client = new WordClient(http);

        await Assert.ThrowsAsync<NotSupportedException>(() => client.Convert(Contract(),
            new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings { PageOrientation = PageOrientation.Landscape } }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.Convert(Contract(),
            new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings { Margins = 36f } }));
        Assert.That(handler.Requests, Is.Empty);
    }

    /// <summary>
    /// The API merges the finished documents without their options, so it starts neither a padded input nor the one after
    /// it on an odd page: a padded input among several fails before anything is sent, wherever it stands, and a padded
    /// input merged alone is merged.
    /// </summary>
    [Test]
    public async Task Merge_Rejects_A_Padded_Input_Among_Several()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };
        var client = new WordClient(http);
        WordTemplateInput Input(bool padded) => new()
        {
            Template = new BinaryFileItem { Bytes = Encoding.UTF8.GetBytes("document") },
            Options = new InputOptions { EnforceEvenAmountOfPages = padded }
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => client.Merge([Input(true), Input(false)]));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.Merge([Input(false), Input(true)]));
        Assert.That(handler.Requests, Is.Empty);

        await client.Merge([Input(true)]);
        Assert.That(handler.Requests.Select(request => request.PathAndQuery), Is.EqualTo(new[] { "/word/create", "/word/merge" }));
    }

    private static WordTemplateInput Contract() => new()
    {
        Template = new BinaryFileItem { Bytes = Encoding.UTF8.GetBytes("contract") },
        GlobalParameters = new Dictionary<string, object> { ["ClientName"] = "ACME" },
        Images = [new WordImage { Name = "logo", File = new BinaryFileItem { Bytes = [1, 2, 3] }, Size = new ImageSize(200, 80), HorizontalAlignment = HorizontalAlignment.Center }],
        DocumentParameters = new Dictionary<string, WordTemplateInput>
        {
            ["addendum"] = new() { Template = new BinaryFileItem { Bytes = Encoding.UTF8.GetBytes("addendum") }, Options = new InputOptions { InheritFont = true } }
        },
        Footers =
        [
            new WordHeaderFooterInput
            {
                Template = new() { Template = new BinaryFileItem { Bytes = Encoding.UTF8.GetBytes("footer") }, Options = new InputOptions { HorizontalAlignment = HorizontalAlignment.Right } }
            }
        ],
        Options = new InputOptions { RemoveEmptyParagraphs = true, EnforceEvenAmountOfPages = true }
    };

    private sealed record RecordedRequest(string PathAndQuery, string? Body, IDictionary<string, string> Fields, IList<string> Files);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var fields = new Dictionary<string, string>();
            var files = new List<string>();
            string? body = null;
            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    var disposition = part.Headers.ContentDisposition!;
                    var name = disposition.Name!.Trim('"');
                    if (disposition.FileName != null)
                    {
                        files.Add(name);
                    }
                    else
                    {
                        fields[name] = await part.ReadAsStringAsync(cancellationToken);
                    }
                }
            }
            else if (request.Content != null)
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            Requests.Add(new RecordedRequest(request.RequestUri!.PathAndQuery, body, fields, files));

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("document"u8.ToArray()) };
        }
    }
}
