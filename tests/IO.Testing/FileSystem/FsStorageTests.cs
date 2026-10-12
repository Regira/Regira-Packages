using IO.Testing.Helpers;
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
    public async Task List() => await StorageTestContext.Test_List();
    [Test]
    public async Task GetBytes() => await StorageTestContext.Test_GetBytes();
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