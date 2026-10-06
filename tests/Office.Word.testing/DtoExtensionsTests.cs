using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Dimensions;
using Regira.Office.Word.Models;
using Regira.Office.Word.Models.DTO;

namespace Office.Word.testing;

/// <summary>
/// <c>DtoExtensions</c> shape the JSON an Office client sends, and an Office API reads it back with the same
/// extensions, so whatever a round trip loses never reaches the backend. What the client puts on the wire is pinned
/// in <c>Office.Clients.Testing</c> (<c>WordClientRequestTests</c>).
/// </summary>
[TestFixture]
public class DtoExtensionsTests
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    [Test]
    public void A_Round_Trip_Keeps_The_Whole_Input()
    {
        var input = Contract();

        var received = input.ToWordDocumentInputDto().ToWordTemplateInput();

        AssertSameInput(received, input, "input");
    }

    [Test]
    public void A_Dto_Read_And_Sent_On_Serializes_The_Same()
    {
        var dto = Contract().ToWordDocumentInputDto();

        var again = dto.ToWordTemplateInput().ToWordDocumentInputDto();

        Assert.That(JsonSerializer.Serialize(again, Json), Is.EqualTo(JsonSerializer.Serialize(dto, Json)));
    }

    [Test]
    public void Absent_Options_Read_As_The_Defaults()
    {
        var dto = Contract().ToWordDocumentInputDto();
        dto.Options = null;

        Assert.That(dto.ToWordTemplateInput().Options, Is.EqualTo(new InputOptions()));
    }

    [Test]
    public void A_Size_Needs_Both_Width_And_Height()
    {
        var dto = Contract().ToWordDocumentInputDto();
        var logo = dto.Images!.First();
        logo.Height = null;

        Assert.That(dto.ToWordTemplateInput().Images!.First().Size, Is.Null);
    }

    private static WordTemplateInput Contract() => new()
    {
        Template = File("contract"),
        GlobalParameters = new Dictionary<string, object> { ["ClientName"] = "ACME", ["NoticeDays"] = 30 },
        CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
        {
            ["ServiceLines"] =
            [
                new Dictionary<string, object> { ["Description"] = "Hosting", ["Quantity"] = 12, ["UnitPrice"] = "40.00" },
                new Dictionary<string, object> { ["Description"] = "Support", ["Quantity"] = 4, ["UnitPrice"] = "95.00" }
            ]
        },
        Images =
        [
            new WordImage { Name = "logo", File = File("logo.png"), Size = new ImageSize(200, 80), HorizontalAlignment = HorizontalAlignment.Center },
            new WordImage { Name = "signature", File = File("signature.png") }
        ],
        DocumentParameters = new Dictionary<string, WordTemplateInput>
        {
            ["addendum"] = new()
            {
                Template = File("addendum"),
                GlobalParameters = new Dictionary<string, object> { ["ClientName"] = "ACME" },
                DocumentParameters = new Dictionary<string, WordTemplateInput>
                {
                    ["annex"] = new() { Template = File("annex"), Options = new InputOptions { HorizontalAlignment = HorizontalAlignment.Justify } }
                },
                Options = new InputOptions { InheritFont = true }
            }
        },
        Headers =
        [
            new WordHeaderFooterInput { Template = new() { Template = File("header"), GlobalParameters = new Dictionary<string, object> { ["ClientName"] = "ACME" } } },
            new WordHeaderFooterInput
            {
                Template = new() { Template = File("first-page-header"), Images = [new WordImage { Name = "logo", File = File("logo-large.png"), Size = new ImageSize(400, 160) }] },
                Type = HeaderFooterType.FirstPage
            }
        ],
        Footers =
        [
            new WordHeaderFooterInput
            {
                Template = new() { Template = File("footer"), Options = new InputOptions { HorizontalAlignment = HorizontalAlignment.Right } },
                Type = HeaderFooterType.Even
            }
        ],
        Options = new InputOptions { RemoveEmptyParagraphs = true, EnforceEvenAmountOfPages = true }
    };

    private static IMemoryFile File(string content) => new BinaryFileItem { Bytes = Encoding.UTF8.GetBytes(content) };

    private static void AssertSameInput(WordTemplateInput actual, WordTemplateInput expected, string path)
    {
        Assert.That(actual.Template.GetBytes(), Is.EqualTo(expected.Template.GetBytes()), $"{path}.Template");
        Assert.That(actual.GlobalParameters, Is.EqualTo(expected.GlobalParameters), $"{path}.GlobalParameters");
        Assert.That(actual.CollectionParameters, Is.EqualTo(expected.CollectionParameters), $"{path}.CollectionParameters");
        Assert.That(actual.Images?.Select(Describe), Is.EqualTo(expected.Images?.Select(Describe)), $"{path}.Images");
        Assert.That(actual.Options, Is.EqualTo(expected.Options), $"{path}.Options");

        Assert.That(actual.DocumentParameters?.Keys.Order(), Is.EqualTo(expected.DocumentParameters?.Keys.Order()), $"{path}.DocumentParameters");
        foreach (var (key, nested) in expected.DocumentParameters ?? new Dictionary<string, WordTemplateInput>())
        {
            AssertSameInput(actual.DocumentParameters![key], nested, $"{path}.DocumentParameters[{key}]");
        }

        AssertSameStories(actual.Headers, expected.Headers, $"{path}.Headers");
        AssertSameStories(actual.Footers, expected.Footers, $"{path}.Footers");
    }

    private static void AssertSameStories(ICollection<WordHeaderFooterInput>? actual, ICollection<WordHeaderFooterInput>? expected, string path)
    {
        Assert.That(actual?.Select(story => story.Type), Is.EqualTo(expected?.Select(story => story.Type)), path);
        if (actual == null || expected == null)
        {
            return;
        }
        foreach (var (story, index) in actual.Zip(expected).Select((pair, index) => (pair, index)))
        {
            AssertSameInput(story.First.Template, story.Second.Template, $"{path}[{index}]");
        }
    }

    private static (string Name, string Bytes, ImageSize? Size, HorizontalAlignment? HorizontalAlignment) Describe(WordImage image)
        => (image.Name, Convert.ToBase64String(image.File!.GetBytes()!), image.Size, image.HorizontalAlignment);
}
