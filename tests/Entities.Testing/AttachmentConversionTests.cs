using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.IO.Models;

namespace Entities.Testing;

/// <summary>
/// <c>ToAttachment(src)</c> copies a file onto the attachment it is given, so an attachment the caller already
/// holds keeps what the file does not carry — its key and its stamps.
/// </summary>
[TestFixture]
public class AttachmentConversionTests
{
    [Test]
    public void ToAttachment_Fills_The_Attachment_It_Is_Given()
    {
        var created = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new Attachment { Id = 7, FileName = "scan.pdf", Created = created };
        var file = new BinaryFileItem { FileName = "archive/2026/scan.pdf", Bytes = [1, 2, 3], Length = 3 };

        var attachment = file.ToAttachment(existing);

        Assert.Multiple(() =>
        {
            Assert.That(attachment, Is.SameAs(existing));
            Assert.That(attachment.Id, Is.EqualTo(7));
            Assert.That(attachment.Created, Is.EqualTo(created));
            Assert.That(attachment.FileName, Is.EqualTo("archive/2026/scan.pdf"));
            Assert.That(attachment.Bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(attachment.Length, Is.EqualTo(3));
        });
    }
}
