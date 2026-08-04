using Regira.Office.Mail.SendGrid.Extensions;
using Regira.Office.Mail.Web;
using Regira.Serializing.Abstractions;
using Regira.Serializing.Newtonsoft.Json;

namespace Office.Mail.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class SendGridMessageObjectTests
{
    private readonly ISerializer _serializer = new JsonSerializer();

    [TestCase(MailConstants.SIMPLE_INPUT)]
    [TestCase(MailConstants.INPUT_NO_SENDER)]
    [TestCase(MailConstants.INPUT_NO_RECIPIENTS)]
    [TestCase(MailConstants.EXTENDED_INPUT)]
    [TestCase(MailConstants.INPUT_NO_SUBJECT)]
    [TestCase(MailConstants.INPUT_NO_BODY)]
    public void Create_MailMessage(string serializedInput)
    {
        var input = _serializer.Deserialize<MailInput>(serializedInput)!;
        var msg = input.ToMessageObject();
        // added [assembly: InternalsVisibleTo("Mail.Testing")] to internal Sendgrid extensions
        var sgMsg = msg.ToMailMessage();

        Assert.That(sgMsg.From?.Email, Is.EqualTo(msg.From?.Email));
        Assert.That(sgMsg.From?.Name, Is.EqualTo(msg.From?.DisplayName));

        Assert.That(sgMsg.ReplyTo?.Email, Is.EqualTo(msg.ReplyTo?.Email));
        Assert.That(sgMsg.ReplyTo?.Name, Is.EqualTo(msg.ReplyTo?.DisplayName));

        // Cannot test recipients -> no such getter on SendGrid's MailMessage object

        Assert.That(sgMsg.Subject, Is.EqualTo(msg.Subject));
        if (msg.IsHtml)
        {
            Assert.That(sgMsg.HtmlContent, Is.EqualTo(msg.Body));
            Assert.That(sgMsg.PlainTextContent, Is.Null);
        }
        else
        {
            Assert.That(sgMsg.PlainTextContent, Is.EqualTo(msg.Body));
            Assert.That(sgMsg.HtmlContent, Is.Null);
        }
    }
}