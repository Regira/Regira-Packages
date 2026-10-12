using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

/// <summary>With an extension filter, the file system lists the folders holding a matching file, and no other.</summary>
[TestFixture]
public class FsFolderFilterTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("folder-filter-test-").FullName;
        Directory.CreateDirectory(Path.Combine(_root, "dir2", "dir2.1"));
        Directory.CreateDirectory(Path.Combine(_root, "dir2", "dir2.10"));
        File.WriteAllText(Path.Combine(_root, "dir2", "dir2.1", "a.log"), "a");
        File.WriteAllText(Path.Combine(_root, "dir2", "dir2.10", "b.txt"), "b");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Test]
    public async Task A_Folder_Whose_Name_Begins_Another_Is_Not_Listed_For_Its_Files()
    {
        var service = new BinaryFileService(new FileSystemOptions { RootFolder = _root });

        var folders = await service.List(new FileSearchObject { Recursive = true, Extensions = [".txt"], Type = FileEntryTypes.Directories });

        Assert.That(folders, Is.EquivalentTo(new[] { "dir2", Path.Combine("dir2", "dir2.10") }));
    }
}
