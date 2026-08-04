using Newtonsoft.Json;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;
using Regira.Office.Mail.MSGReader;

namespace Office.Mail.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MsgReaderTests
{
    private readonly string _assets;
    private readonly IMessageObject _sampleMessageObject;
    public MsgReaderTests()
    {
        _assets = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "../../../Assets")).FullName;
        var sampleObjectPath = Path.Combine(_assets, "serialized-sample.json");
        _sampleMessageObject = JsonConvert.DeserializeObject<MessageObject>(File.ReadAllText(sampleObjectPath))!;
    }


    [Test]
    public async Task Parse_MSG()
    {
        var msgPath = Path.Combine(_assets, "sample.msg");
        using var msgFile = new BinaryFileItem(msgPath);
        var parser = new MsgParser();
        var msgObj = await parser.Parse(msgFile.GetBytes()!.ToMemoryFile());

        Assert.That(msgObj.From!.Email, Is.EqualTo(_sampleMessageObject.From!.Email));
        Assert.That(string.IsNullOrWhiteSpace(msgObj.From!.DisplayName), Is.True);
        Assert.That(msgObj.Body, Is.EqualTo(_sampleMessageObject.Body));
        Assert.That(msgObj.To.Select(x => x.Email), Is.EquivalentTo(_sampleMessageObject.To.Select(x => x.Email)));
        var msgAttachments = msgObj.Attachments!.ToArray();
        var sampleAttachments = _sampleMessageObject.Attachments!.ToArray();
        for (var i = 0; i < msgObj.Attachments!.Count; i++)
        {
            Assert.That(msgAttachments[i].FileName, Is.EqualTo(sampleAttachments[i].FileName));
            Assert.That(msgAttachments[i].Bytes, Is.EquivalentTo(sampleAttachments[i].Bytes!));
        }
    }
    [Test]
    public async Task Parse_EML()
    {
        var emlPath = Path.Combine(_assets, "sample.eml");
        using var emlFile = new BinaryFileItem(emlPath);
        var parser = new EmlParser();
        var msgObj = await parser.Parse(emlFile.GetBytes()!.ToMemoryFile());

        Assert.That(msgObj.From!.Email, Is.EqualTo(_sampleMessageObject.From!.Email));
        //Assert.That(string.IsNullOrWhiteSpace(msgObj.From!.DisplayName), Is.True);
        Assert.That(msgObj.Body, Is.EqualTo(_sampleMessageObject.Body));
        Assert.That(msgObj.To.Select(x => x.Email), Is.EquivalentTo(_sampleMessageObject.To.Select(x => x.Email)));
        var msgAttachments = msgObj.Attachments!.ToArray();
        var sampleAttachments = _sampleMessageObject.Attachments!.ToArray();
        for (var i = 0; i < msgObj.Attachments!.Count; i++)
        {
            Assert.That(msgAttachments[i].FileName, Is.EqualTo(sampleAttachments[i].FileName));
            Assert.That(msgAttachments[i].Bytes, Is.EquivalentTo(sampleAttachments[i].Bytes!));
        }
    }
}