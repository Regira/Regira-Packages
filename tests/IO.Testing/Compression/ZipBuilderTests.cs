using IO.Testing.Helpers;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.IO.Storage.Compression;
using Regira.IO.Utilities;

namespace IO.Testing.Compression;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ZipBuilderTests
{
    private BinaryFileItem[] _sourceFiles = null!;
    private readonly string _assetsDir;
    public ZipBuilderTests()
    {
        _assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "assets", "zip-builder");
        Directory.CreateDirectory(_assetsDir);
    }

    [SetUp]
    public void Setup()
    {
        _sourceFiles = TestFilesCreator.Create(_assetsDir)
            .ToArray();
    }
    [TearDown]
    public void Teardown()
    {
        TestFilesCreator.Clear(_assetsDir);
    }

    [Test]
    public async Task Can_Zip_Files()
    {
        var zipBuilder = new ZipBuilder();
        using var zipFile = await zipBuilder
            .For(_sourceFiles)
            .Build();
        Assert.That(zipFile.GetBytes()!, Is.Not.Empty);
        Assert.That(zipFile.GetLength() > 0, Is.True);
    }
    [Test]
    public async Task Can_Add_Files_To_Existing_Zip()
    {
        var zipBuilder1 = new ZipBuilder();
        var files1 = _sourceFiles.Take(5).ToArray();
        var files2 = _sourceFiles.Except(files1).ToArray();
        using var zipFile = await zipBuilder1
            .For(files1)
            .Build();
        Assert.That(ZipUtility.Unzip(zipFile.ToBinaryFile()).Count, Is.EqualTo(files1.Length));
        var zipStream1Length = zipFile.Stream?.Length;
        var zipBuilder2 = new ZipBuilder();
        await zipBuilder2
            .For(zipFile.Stream)
            .For(files2)
            .Build();
        Assert.That(zipFile.Stream?.Length > zipStream1Length, Is.True);
        Assert.That(ZipUtility.Unzip(zipFile.ToBinaryFile()).Count, Is.EqualTo(_sourceFiles.Length));
    }

    [Test]
    public async Task Can_Unzip_Files()
    {
        var zipBuilder = new ZipBuilder();
        using var zipFile = await zipBuilder
            .For(_sourceFiles)
            .Build();
        //FileSystemUtility.SaveStream("zipped.zip", zipStream);
        var unzippedFiles = ZipUtility.Unzip(zipFile.ToBinaryFile());
        // compare count
        Assert.That(unzippedFiles.Count, Is.EqualTo(_sourceFiles.Length));
        // compare bytes
        Assert.That(unzippedFiles.Select(f => FileUtility.GetBytes(f.Stream)), Is.EqualTo(_sourceFiles.Select(f => f.Bytes)).AsCollection);
        unzippedFiles.Dispose();
    }
}