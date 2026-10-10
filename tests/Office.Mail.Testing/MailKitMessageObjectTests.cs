using MimeKit;
using Regira.IO.Models;
using Regira.Office.Mail.Exceptions;
using Regira.Office.Mail.MailKit.Extensions;
using Regira.Office.Mail.Models;
using Regira.Office.Mail.Web;
using Regira.Serializing.Abstractions;
using Regira.Serializing.Newtonsoft.Json;

namespace Office.Mail.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MailKitMessageObjectTests
{
    private readonly ISerializer _serializer = new JsonSerializer();

    [TestCase(MailConstants.SIMPLE_INPUT)]
    [TestCase(MailConstants.INPUT_REPLYTO)]
    [TestCase(MailConstants.EXTENDED_INPUT)]
    [TestCase(MailConstants.INPUT_NO_HMTL)]
    [TestCase(MailConstants.INPUT_NO_SUBJECT)]
    [TestCase(MailConstants.INPUT_NO_BODY)]
    public void Create_MimeMessage(string serializedInput)
    {
        var msg = _serializer.Deserialize<MailInput>(serializedInput)!.ToMessageObject();

        var mime = msg.ToMimeMessage();

        var from = mime.From.Mailboxes.Single();
        Assert.That(from.Address, Is.EqualTo(msg.From!.Email));
        Assert.That(from.Name, Is.EqualTo(msg.From.DisplayName));
        Assert.That(mime.ReplyTo.Mailboxes.Select(x => x.Address), Is.EqualTo(msg.ReplyTo == null ? Array.Empty<string>() : new[] { msg.ReplyTo.Email }));
        Assert.That(mime.To.Mailboxes.Select(x => x.Address), Is.EqualTo(msg.To.Select(x => x.Email)));
        Assert.That(mime.Subject, Is.EqualTo(msg.Subject ?? string.Empty));
        if (msg.IsHtml)
        {
            Assert.That(mime.HtmlBody, Is.EqualTo(msg.Body ?? string.Empty));
            Assert.That(mime.TextBody, Is.Null);
        }
        else
        {
            Assert.That(mime.TextBody, Is.EqualTo(msg.Body ?? string.Empty));
            Assert.That(mime.HtmlBody, Is.Null);
        }
    }

    [Test]
    public void Recipients_By_Type_With_Display_Names()
    {
        var msg = new MessageObject
        {
            From = new MailAddress { Email = "orders@myshop.com", DisplayName = "MyShop" },
            ReplyTo = new MailAddress { Email = "support@myshop.com", DisplayName = "Support" },
            To =
            {
                new MailRecipient { Email = "alice@example.com", DisplayName = "Alice" },
                new MailRecipient { Email = "sales@myshop.com", RecipientType = RecipientTypes.Cc },
                new MailRecipient { Email = "audit@myshop.com", DisplayName = "Audit", RecipientType = RecipientTypes.Bcc }
            },
            Subject = "Order confirmation #42",
            Body = "<p>Thanks</p>"
        };

        var mime = msg.ToMimeMessage();

        Assert.That(Format(mime.From), Is.EqualTo("MyShop <orders@myshop.com>"));
        Assert.That(Format(mime.ReplyTo), Is.EqualTo("Support <support@myshop.com>"));
        Assert.That(Format(mime.To), Is.EqualTo("Alice <alice@example.com>"));
        Assert.That(Format(mime.Cc), Is.EqualTo("sales@myshop.com"));
        Assert.That(Format(mime.Bcc), Is.EqualTo("Audit <audit@myshop.com>"));
    }

    [Test]
    public void Attachment_Keeps_Name_And_Content_Type()
    {
        var msg = new MessageObject
        {
            From = (MailAddress)"orders@myshop.com",
            To = { (MailRecipient)"alice@example.com" },
            Body = "Invoice attached",
            IsHtml = false,
            Attachments =
            [
                new BinaryFileItem { FileName = "invoice-42.pdf", Bytes = [37, 80, 68, 70] },
                new BinaryFileItem { FileName = "data.bin", Bytes = [1, 2, 3], ContentType = "text/csv" }
            ]
        };

        var attachments = msg.ToMimeMessage().Attachments.OfType<MimePart>().ToArray();

        Assert.That(attachments.Select(x => x.FileName), Is.EqualTo(new[] { "invoice-42.pdf", "data.bin" }));
        Assert.That(attachments.Select(x => x.ContentType.MimeType), Is.EqualTo(new[] { "application/pdf", "text/csv" }));
    }

    [TestCase(MailConstants.INPUT_NO_SENDER)]
    [TestCase(MailConstants.INPUT_NO_RECIPIENTS)]
    public void Without_Sender_Or_Recipients_Throws(string serializedInput)
    {
        var msg = _serializer.Deserialize<MailInput>(serializedInput)!.ToMessageObject();

        Assert.Throws<MailException>(() => msg.ToMimeMessage());
    }

    private static string Format(InternetAddressList list)
        => string.Join(", ", list.Mailboxes.Select(x => string.IsNullOrEmpty(x.Name) ? x.Address : $"{x.Name} <{x.Address}>"));
}
