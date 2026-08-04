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

    [Test]
    public void FileService_Traversal_Throws()
    {
        var svc = new BinaryFileService(new FileSystemOptions { RootFolder = _root });
        Assert.Throws<UnauthorizedAccessException>(() => svc.Exists("../../outside.txt"));
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

    // --- ZipUtility (Zip Slip) ---

    [Test]
    public void ZipUtility_Safe_Entry_Does_Not_Throw()
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("subfolder/file.txt").Open());
            writer.Write("hello");
        }
        ms.Position = 0;
        using var readArchive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        Assert.DoesNotThrow(() => ZipUtility.ExtractFiles(_root, readArchive.Entries));
    }

    [Test]
    public void ZipUtility_Traversal_Entry_Throws()
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../../evil.txt").Open());
            writer.Write("evil");
        }
        ms.Position = 0;
        using var readArchive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Throws<UnauthorizedAccessException>(() => ZipUtility.ExtractFiles(_root, readArchive.Entries));
    }
}
