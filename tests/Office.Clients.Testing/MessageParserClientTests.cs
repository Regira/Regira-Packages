using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Models;
using Regira.Office.Mail.Abstractions;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MessageParserClientTests : OfficeClientTestsBase
{
    private readonly string _inputDir;

    private IMessageParser _service = null!;

    public MessageParserClientTests()
    {
        var testsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        _inputDir = Path.Combine(testsDir, "Office.Mail.Testing", "Assets");
    }

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<IMessageParser>();
    }

    [Test]
    public async Task Parse_Msg()
    {
        var file = new BinaryFileItem
        {
            Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "sample.msg")),
            FileName = "sample.msg"
        };

        var result = await _service.Parse(file);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Subject, Is.Not.Null);
    }

    [Test]
    public async Task Parse_Eml()
    {
        var file = new BinaryFileItem
        {
            Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "sample.eml")),
            FileName = "sample.eml"
        };

        var result = await _service.Parse(file);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Subject, Is.Not.Null);
    }

    [Test]
    public async Task Parse_Msg_HasFrom()
    {
        var file = new BinaryFileItem
        {
            Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "sample.msg")),
            FileName = "sample.msg"
        };

        var result = await _service.Parse(file);

        Assert.That(result.From, Is.Not.Null);
        Assert.That(result.From!.Email, Is.Not.Null);
    }
}
