using System.IO.Compression;
using Regira.IO.Storage.Compression;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

[TestFixture]
public class ContainmentTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
        => _root = Directory.CreateTempSubdirectory("containment-test-").FullName;

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    // --- BinaryFileService ---

    [Test]
    public void FileService_Safe_Path_Does_Not_Throw()
    {
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = _root });
        Assert.DoesNotThrow(() => svc.Exists("subfolder/file.txt"));
    }

    [TestCase("../../outside.txt")]
    [TestCase(@"..\..\outside.txt")]
    [TestCase(@"subfolder/..\..\outside.txt")]
    [TestCase("/outside.txt")]
    public void FileService_Traversal_Throws(string identifier)
    {
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = _root });
        Assert.Throws<UnauthorizedAccessException>(() => svc.Exists(identifier));
    }

    [Test]
    public void FileService_Save_Traversal_Throws()
    {
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = _root });
        Assert.Throws<UnauthorizedAccessException>(() => svc.Save("../../outside.txt", Array.Empty<byte>()));
    }

    [Test]
    public void FileService_Contained_False_Allows_Traversal()
    {
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = _root, Contained = false });
        Assert.DoesNotThrow(() => svc.Exists("../../outside.txt"));
    }

    [Test]
    public void EnsureContained_Compares_Folder_Names_As_The_File_System_Does()
    {
        // "../uploads" from "Uploads": the same folder where names ignore case, its twin where they do not
        var root = Path.Combine(_root, "Uploads");
        var path = Path.Combine(root, "..", "uploads", "report.txt");

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.DoesNotThrow(() => FileNameUtility.EnsureContained(path, root));
        }
        else
        {
            Assert.Throws<UnauthorizedAccessException>(() => FileNameUtility.EnsureContained(path, root));
        }
    }

    [Test]
    public void EnsureContained_Refuses_A_Sibling_Whose_Name_Starts_With_The_Root()
    {
        // "Uploads-archive" starts with "Uploads" but is not inside it
        var root = Path.Combine(_root, "Uploads");
        var path = Path.Combine(_root, "Uploads-archive", "report.txt");

        Assert.Throws<UnauthorizedAccessException>(() => FileNameUtility.EnsureContained(path, root));
    }

    // --- ZipUtility (Zip Slip) ---

    [TestCase("subfolder/file.txt")]
    [TestCase(@"subfolder\nested/file.txt")]
    [TestCase("subfolder/../file.txt")]
    public void ZipUtility_Safe_Entry_Does_Not_Throw(string entryName)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write("hello");
        }
        ms.Position = 0;
        using var readArchive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        Assert.DoesNotThrow(() => ZipUtility.ExtractFiles(_root, readArchive.Entries));
    }

    [TestCase("../../evil.txt")]
    [TestCase(@"..\..\evil.txt")]
    [TestCase(@"../..\evil.txt")]
    [TestCase(@"subfolder/..\..\evil.txt")]
    [TestCase("/evil.txt")]
    [TestCase(@"\evil.txt")]
    public void ZipUtility_Traversal_Entry_Throws(string entryName)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write("evil");
        }
        ms.Position = 0;
        using var readArchive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Throws<UnauthorizedAccessException>(() => ZipUtility.ExtractFiles(_root, readArchive.Entries));
    }

    // a drive letter roots an entry on Windows only; elsewhere "C:" is a folder name like any other
    [TestCase("C:/evil.txt")]
    [TestCase(@"C:\evil.txt")]
    [TestCase("C:evil.txt")]
    public void ZipUtility_Drive_Letter_Entry_Throws_On_Windows(string entryName)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("A drive letter roots a path on Windows only.");
        }
        ZipUtility_Traversal_Entry_Throws(entryName);
    }
}
