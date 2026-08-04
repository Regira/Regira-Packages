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
    public async Task ListAsync() => await StorageTestContext.Test_ListAsync();
    [Test]
    public async Task Filter_By_Folder_Async() => await StorageTestContext.Test_Filter_By_Folder_Async();
    [Test]
    public async Task Filter_By_Extension_Async() => await StorageTestContext.Test_Filter_By_Extension_Async();
    [Test]
    public async Task Filter_Recursive_Async() => await StorageTestContext.Test_Filter_Recursive_Async();
    [Test]
    public async Task Filter_By_EntryType_Async() => await StorageTestContext.Test_Filter_By_EntryType_Async();
#endif

    [Test]
    public async Task Add_File() => await StorageTestContext.Test_Add_File();
    [Test]
    public async Task Update_File() => await StorageTestContext.Test_Update_File();
    [Test]
    public async Task Remove_File() => await StorageTestContext.Test_Remove_File();
}