using System.Reflection;
using Regira.IO.Models;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Office.Mail.Testing.Abstractions;

public abstract class MailerTestsBase
{
    protected IMailService Mailer = null!;
    protected readonly string Assets = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "../../..", "Assets")).FullName;
    // ReSharper disable AssignNullToNotNullAttribute
    // ReSharper restore AssignNullToNotNullAttribute

    public virtual async Task Send_Without_Attachment()
    {
        var msg = new MessageObject
        {
            From = (MailAddress)"bram@regira.com",
            To = { (MailRecipient)"bramverboven@hotmail.com" },
            Subject = $"Test from {Mailer.GetType().Name}",
            Body = "Testing without attachment..."
        };
        var response = await Mailer.Send(msg);

        Assert.That(response.Success, Is.True);
    }
    public virtual async Task Send_With_Attachment()
    {
        var attachment = new BinaryFileItem
        {
            FileName = "file1.pdf",
            Bytes = await File.ReadAllBytesAsync(Path.Combine(Assets, "file1.pdf"))
        };
        var msg = new MessageObject
        {
            From = (MailAddress)"bram@regira.com",
            To = { (MailRecipient)"bramverboven@hotmail.com" },
            Subject = $"Test from {Mailer.GetType().Name}",
            Body = "Testing with attachment...",
            Attachments = new List<BinaryFileItem> { attachment }
        };
        var response = await Mailer.Send(msg);

        Assert.That(response.Success, Is.True);
    }
}