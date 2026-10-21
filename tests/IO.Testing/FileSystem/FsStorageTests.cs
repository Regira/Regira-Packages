using IO.Testing.Helpers;
using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class FsStorageTests
{
    public StorageTestHelper.StorageTestContext<BinaryFileService> StorageTestContext { get; set; }

    [SetUp]
    public void Setup() => StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, folder)
        => new BinaryFileService(new FileSystemOptions { RootFolder = folder }));
    [TearDown]
    public async Task TearDown() => await StorageTestContext.DisposeAsync();

    [Test]
    public async Task List_Answers_The_Identifiers_Save_Does()
    {
        var root = Path.Combine(Path.GetTempPath(), "regira-fs-list-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new BinaryFileService(new FileSystemOptions { RootFolder = root });
            var saved = new[]
            {
                await service.Save("docs/a.txt", "a"u8.ToArray()),
                await service.Save("docs/sub/b.txt", "b"u8.ToArray())
            };

            var files = await service.List(new FileSearchObject { Recursive = true, Type = FileEntryTypes.Files });
            var folders = await service.List(new FileSearchObject { Recursive = true, Type = FileEntryTypes.Directories });

            // '/' on every platform, as Save and GetIdentifier answer, so an identifier compares and splits the same everywhere
            Assert.That(files, Is.EquivalentTo(saved));
            Assert.That(folders, Is.EquivalentTo(new[] { "docs", "docs/sub" }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task List() => await StorageTestContext.Test_List();
    [Test]
    public async Task GetBytes() => await StorageTestContext.Test_GetBytes();
    [Test]
    public async Task GetInfo() => await StorageTestContext.Test_GetInfo();
    [Test]
    public async Task Filter_By_Folder() => await StorageTestContext.Test_Filter_By_Folder();
    [Test]
    public async Task Filter_By_Extension() => await StorageTestContext.Test_Filter_By_Extension();
    [Test]
    public async Task Filter_Recursive() => await StorageTestContext.Test_Filter_Recursive();
    [Test]
    public async Task Filter_By_EntryType() => await StorageTestContext.Test_Filter_By_EntryType();

#if NET10_0_OR_GREATER
    [Test]
    public async Task ListAsync_Yields_What_List_Returns() => await StorageTestContext.Test_ListAsync_Yields_What_List_Returns();
#endif

    [Test]
    public async Task Add_File() => await StorageTestContext.Test_Add_File();
    [Test]
    public async Task Update_File() => await StorageTestContext.Test_Update_File();
    [Test]
    public async Task Update_File_With_Shorter_Content() => await StorageTestContext.Test_Update_File_With_Shorter_Content();
    [Test]
    public async Task Remove_File() => await StorageTestContext.Test_Remove_File();
}