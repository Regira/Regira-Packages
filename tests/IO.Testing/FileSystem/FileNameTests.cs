using Moq;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.FileSystem;
using Regira.IO.Storage.Helpers;

namespace IO.Testing.FileSystem;

[TestFixture]
public class FileNameTests
{
    [TestCase("image.jpeg", "image.jpeg")]
    [TestCase("in\"v<a>li|d.jpeg", "in_v_a_li_d.jpeg")]
    [TestCase("in\"v<a>li|d\\image.jpeg", "in_v_a_li_d\\image.jpeg")]
    [TestCase("CLOCK$\\image.jpeg", "_XXX_\\image.jpeg")]
    [TestCase("COM5\\LPT5\\image.jpeg", "_XXX_\\_XXX_\\image.jpeg")]
    [TestCase("xCOM5\\xLPT5\\image.jpeg", "xCOM5\\xLPT5\\image.jpeg")]
    public void Sanitize_FileName(string input, string? expected)
    {
        var sanitized = FileNameUtility.SanitizeFilename(input);
        Assert.That(sanitized, Is.EqualTo(expected));
    }

    [Test]
    public async Task Next_Available_FileName()
    {
        var file = "folder/file.jpg";
        var fileService = new Mock<IFileService>();
        fileService.Setup(l => l.Exists(file)).ReturnsAsync(false);

        var fh = new FileNameHelper(fileService.Object);
        var identifier = await fh.NextAvailableFileName(file);

        Assert.That(identifier, Is.EqualTo(file));
    }
    [Test]
    public async Task Next_Available_FileName_Increase_Number()
    {
        var file1 = "folder/file.jpg";
        var file2 = "folder/file-(2).jpg";
        var file3 = "folder/file-(3).jpg";
        var fileService = new Mock<IFileService>();
        fileService.Setup(l => l.Exists(file1)).ReturnsAsync(true);
        fileService.Setup(l => l.Exists(file2)).ReturnsAsync(true);
        fileService.Setup(l => l.Exists(file3)).ReturnsAsync(false);

        var fh = new FileNameHelper(fileService.Object);
        var identifier = await fh.NextAvailableFileName(file1);

        Assert.That(identifier, Is.EqualTo(file3));
    }
    [Test]
    public void EnsureContained_Safe_Path_Returns_Normalized()
    {
        var root = Path.Combine(Path.GetTempPath(), "test-root");
        var path = Path.Combine(root, "subfolder", "file.txt");
        Assert.That(FileNameUtility.EnsureContained(path, root), Is.EqualTo(Path.GetFullPath(path)));
    }

    [Test]
    public void EnsureContained_Root_Itself_Returns_Normalized()
    {
        var root = Path.Combine(Path.GetTempPath(), "test-root");
        Assert.That(FileNameUtility.EnsureContained(root, root), Is.EqualTo(Path.GetFullPath(root)));
    }

    [Test]
    public void EnsureContained_Traversal_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "test-root");
        Assert.Throws<UnauthorizedAccessException>(
            () => FileNameUtility.EnsureContained(Path.Combine(root, "..", "outside"), root));
    }

    [Test]
    public void EnsureContained_External_Path_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "test-root");
        var other = Path.Combine(Path.GetTempPath(), "other-root", "file.txt");
        Assert.Throws<UnauthorizedAccessException>(() => FileNameUtility.EnsureContained(other, root));
    }

    [Test]
    public async Task Next_Available_FileName_With_Custom_NumberPattern()
    {
        var file1 = "folder/file.jpg";
        var file2 = "folder/file 2.jpg";
        var file3 = "folder/file 3.jpg";
        var fileService = new Mock<IFileService>();
        fileService.Setup(l => l.Exists(file1)).ReturnsAsync(true);
        fileService.Setup(l => l.Exists(file2)).ReturnsAsync(true);
        fileService.Setup(l => l.Exists(file3)).ReturnsAsync(false);

        var fh = new FileNameHelper(fileService.Object, new FileNameHelper.Options { NumberPattern = @" {0}" });
        var identifier = await fh.NextAvailableFileName(file1);

        Assert.That(identifier, Is.EqualTo(file3));
    }
}