using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.IO.Models;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// <c>FileName</c> is the client's own value and may carry a <b>virtual folder</b>
/// (<c>folder1/folder2/report.pdf</c>) — that is how attachments are organised, and re-filing one is just a
/// different <c>FileName</c>. The storage identifier is generated independently of it, so a "move" never
/// touches the stored bytes.
/// </summary>
[TestFixture]
public class AttachmentFileNameTests
{
    private sealed class ProbeAttachment : EntityAttachment
    {
        public ProbeAttachment() => ObjectType = "Probe";
    }

    private static DefaultFileIdentifierGenerator<int, Attachment> CreateGenerator()
        => new(new AttachmentFileService<Attachment, int>(new BinaryFileService(new FileSystemOptions { RootFolder = Path.GetTempPath() })));

    // --- FileName keeps the folder structure, normalized ---

    [TestCase("folder1/folder2/report.pdf", "folder1/folder2/report.pdf")]
    [TestCase("folder2\\mypicture.jpg", "folder2/mypicture.jpg")]
    [TestCase("/leading/slash/x.txt", "leading/slash/x.txt")]
    [TestCase("trailing/slash/", "trailing/slash")]
    [TestCase("mypicture.jpg", "mypicture.jpg")]
    public void VirtualPath_Keeps_Folders_And_Normalizes_Separators(string input, string expected)
        => Assert.That(input.ToVirtualPath(), Is.EqualTo(expected));

    [TestCase("../../escape.txt", "escape.txt")]
    [TestCase("a/../b/x.txt", "a/b/x.txt")]
    [TestCase("./a//b/x.txt", "a/b/x.txt")]
    public void VirtualPath_Drops_Relative_And_Empty_Segments(string input, string expected)
        => Assert.That(input.ToVirtualPath(), Is.EqualTo(expected));

    [Test]
    public void Null_Stays_Null() => Assert.That(((string?)null).ToVirtualPath(), Is.Null);

    [TestCase("folder1/folder2/report.pdf", "report.pdf")]
    [TestCase("report.pdf", "report.pdf")]
    public void FileNameOnly_Is_The_Last_Segment(string input, string expected)
        => Assert.That(input.ToFileNameOnly(), Is.EqualTo(expected));

    [Test]
    public void Upload_Keeps_The_Supplied_Virtual_Path()
    {
        var file = new BinaryFileItem { FileName = "archive/2026/scan.pdf", Bytes = [1, 2, 3] };

        Assert.That(file.ToAttachment().FileName, Is.EqualTo("archive/2026/scan.pdf"));
    }

    // --- The identifier is internal and carries none of it ---

    [Test]
    public async Task Identifier_Ignores_The_Virtual_Folder()
    {
        var entity = new ProbeAttachment { ObjectId = 42, Attachment = new Attachment { FileName = "archive/2026/scan.pdf" } };

        var identifier = (await CreateGenerator().Generate(entity)).Replace('\\', '/');

        Assert.Multiple(() =>
        {
            Assert.That(identifier, Does.StartWith("Probe/Attachments/42/scan-"));
            Assert.That(identifier, Does.EndWith(".pdf"));
            Assert.That(identifier, Does.Not.Contain("archive"), "the client's folders must not reach storage");
        });
    }

    [Test]
    public async Task Refiling_Does_Not_Change_The_Identifier()
    {
        var attachment = new Attachment { FileName = "archive/2026/scan.pdf" };
        var entity = new ProbeAttachment { ObjectId = 42, Attachment = attachment };
        var identifier = await CreateGenerator().Generate(entity);

        // a "move" is nothing but a different FileName
        attachment.FileName = "archive/2027/scan.pdf".ToVirtualPath();

        Assert.That(await CreateGenerator().Generate(entity), Does.StartWith(identifier[..identifier.LastIndexOf('-')]));
    }
}
