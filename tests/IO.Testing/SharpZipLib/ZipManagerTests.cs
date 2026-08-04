using IO.Testing.Helpers;
using Regira.IO.Compression.SharpZipLib;
using Regira.IO.Storage.FileSystem;
using Regira.IO.Storage.Utilities;

namespace IO.Testing.SharpZipLib;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ZipManagerTests
{
    private readonly string _assetsDir;
    public ZipManagerTests()
    {
        _assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "../../../", "assets", "compression");
        Directory.CreateDirectory(_assetsDir);
    }
        
    [TearDown]
    public void TearDown()
    {
        var dir = new DirectoryInfo(Path.Combine(_assetsDir, "..\\unzipped")).FullName;
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
        TestFilesCreator.Clear(_assetsDir);
    }


    [Test]
    public async Task Create_Zip()
    {
        Assert.Ignore("ToDo");
        var targetDir = new DirectoryInfo(Path.Combine(_assetsDir, "..\\unzipped")).FullName;
        var files = Directory.GetFiles(_assetsDir, "*", SearchOption.AllDirectories);

        var zm = new ZipManager();

        using var fileCollection = await files.ToBinaryFileCollection(FileDataSource.Stream);
        // create zip
        await using var zipStream = zm.Zip(fileCollection);
        await FileSystemUtility.SaveStream(Path.Combine(_assetsDir, "test-zip.zip"), zipStream);

        // unzip
        var fileItems = await zm.Unzip(zipStream);

        // get unzipped files
        var unzippedFiles = fileItems
            .Select(bf => bf.FileName)
            .ToArray();

        // test
        Assert.That(unzippedFiles, Is.Not.Empty);
        Assert.That(unzippedFiles.Length, Is.EqualTo(files.Length));
        var expectedFiles = files.Select(f => f.Substring(_assetsDir.Length)).ToArray();
        var actualFiles = unzippedFiles.Select(f => f?.Substring(targetDir.Length)).ToArray();
        Assert.That(actualFiles, Is.EquivalentTo(expectedFiles));
    }
    [Test]
    public void Update_Zip_Add_Files()
    {
         Assert.Ignore("ToDo");
    }
    [Test]
    public void Update_Zip_Remove_Files()
    {
         Assert.Ignore("ToDo");
    }
}