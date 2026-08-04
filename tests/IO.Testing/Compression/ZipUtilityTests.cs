using System.IO.Compression;
using IO.Testing.Helpers;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.IO.Storage.Compression;

namespace IO.Testing.Compression;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ZipUtilityTests
{
    private readonly string _assetsDir;
    private BinaryFileItem[] _sourceFiles = null!;
    public ZipUtilityTests()
    {
        _assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "assets", "zip-utility");
        Directory.CreateDirectory(_assetsDir);
    }


    [SetUp]
    public void SetUp()
    {
        _sourceFiles = TestFilesCreator.Create(_assetsDir).ToArray();
    }
    [TearDown]
    public void TearDown()
    {
        Directory.Delete(new DirectoryInfo(Path.Combine(_assetsDir, "..\\unzipped")).FullName, true);
        TestFilesCreator.Clear(_assetsDir);
    }


    [Test]
    public void Test_ZipArchive()
    {
        var files = Directory.GetFiles(_assetsDir, "*", SearchOption.AllDirectories);
        var targetDir = new DirectoryInfo(Path.Combine(_assetsDir, "..\\unzipped")).FullName;

        using (var zipStream = new MemoryStream())
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var zipFile in files)
                {
                    var filename = zipFile.Substring(_assetsDir.Length);
                    using var fileStream = File.OpenRead(zipFile);
                    var entry = archive.CreateEntry(filename);
                    using var entryStream = entry.Open();
                    fileStream.Seek(0, SeekOrigin.Begin);
                    fileStream.CopyTo(entryStream);
                }
            }
            zipStream.Position = 0;

            // unzip files to targetDir
            using (var archive2 = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive2.Entries)
                {
                    using var entryStream = entry.Open();
                    var fullPath = Path.Combine(targetDir, entry.FullName.TrimStart('\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    using var fileStream = File.Create(fullPath);
                    entryStream.CopyTo(fileStream);
                }
            }
        }

        var unzippedFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);
        Assert.That(unzippedFiles, Is.Not.Empty);
        Assert.That(unzippedFiles.Length, Is.EqualTo(files.Length));
        var expectedFiles = files.Select(f => f.Substring(_assetsDir.Length)).ToArray();
        var actualFiles = unzippedFiles.Select(f => f.Substring(targetDir.Length)).ToArray();
        Assert.That(actualFiles, Is.EquivalentTo(expectedFiles));
    }

    [Test]
    public void Create_Zip()
    {
        var targetDir = new DirectoryInfo(Path.Combine(_assetsDir, "..\\unzipped")).FullName;
        var files = Directory.GetFiles(_assetsDir, "*", SearchOption.AllDirectories);

        // create zip
        using var zipFile = ZipUtility.Zip(files);

        // unzip
        ZipUtility.Unzip(zipFile.ToBinaryFile(), targetDir);

        // get unzipped files
        var unzippedFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);

        // test
        Assert.That(unzippedFiles, Is.Not.Empty);
        Assert.That(unzippedFiles.Length, Is.EqualTo(files.Length));
        var expectedFiles = files.Select(f => f.Substring(_assetsDir.Length)).ToArray();
        var actualFiles = unzippedFiles.Select(f => f.Substring(targetDir.Length)).ToArray();
        Assert.That(actualFiles, Is.EquivalentTo(expectedFiles));
    }
    [Test]
    public void Update_Zip_Add_Files()
    {
        var targetDir = Path.Combine(_assetsDir, "..\\unzipped");
        var dir1Files = Directory.GetFiles(_assetsDir, "dir1\\*", SearchOption.AllDirectories);
        var dir2Files = Directory.GetFiles(_assetsDir, "dir2\\*", SearchOption.AllDirectories)
            .Select(path => new BinaryFileItem
            {
                Identifier = path.Substring(_assetsDir.Length).TrimStart('\\'),
                Path = path,
                FileName = Path.GetFileName(path),
                Bytes = File.ReadAllBytes(path)
            })
            .ToArray();

        // zip dir1 files
        using var zipFile = ZipUtility.Zip(dir1Files, _assetsDir);
        using var zipStream = zipFile.GetStream()!;

        // add dir2 files
        ZipUtility.AddFiles(zipStream, dir2Files);

        // unzip
        ZipUtility.Unzip(zipStream.ToBinaryFile(), targetDir);

        // get unzipped files
        var unzippedFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);

        // test
        Assert.That(unzippedFiles, Is.Not.Empty);
        Assert.That(unzippedFiles.Length, Is.EqualTo(dir1Files.Length + dir2Files.Length));
        var expectedFiles = dir1Files.Concat(dir2Files.Select(f2 => f2.Path)).Select(f => f?.Substring(_assetsDir.Length)).ToArray();
        var actualFiles = unzippedFiles.Select(f => f.Substring(targetDir.Length)).ToArray();
        Assert.That(actualFiles, Is.EquivalentTo(expectedFiles));
    }
    [Test]
    public void Update_Zip_Remove_Files()
    {
        var targetDir = Path.Combine(_assetsDir, "..\\unzipped");
        var files = Directory.GetFiles(_assetsDir, "*", SearchOption.AllDirectories);
        var dir2Files = _sourceFiles.Where(f => f.Path!.Contains("\\dir2\\")).ToArray();

        // create zip
        using var zipFile = ZipUtility.Zip(files);
        using var zipStream = zipFile.GetStream()!;

        // remove files from zip
        ZipUtility.RemoveFiles(zipStream, dir2Files);

        // unzip
        ZipUtility.Unzip(zipStream.ToBinaryFile(), targetDir);

        // get unzipped files
        var unzippedFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);

        // test
        Assert.That(unzippedFiles, Is.Not.Empty);
        Assert.That(unzippedFiles.Length, Is.EqualTo(files.Length - dir2Files.Length));

        var expectedFiles = files.Where(f => dir2Files.All(f2 => f2.Path != f)).Select(f => f.Substring(_assetsDir.Length)).ToArray();
        var actualFiles = unzippedFiles.Select(f => f.Substring(targetDir.Length)).ToArray();
        Assert.That(actualFiles, Is.EquivalentTo(expectedFiles));
    }
}