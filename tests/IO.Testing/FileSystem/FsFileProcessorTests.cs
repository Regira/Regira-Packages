using IO.Testing.Helpers;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class FsFileProcessorTests
{
    private const string TEST_FOLDER = "file_processor";
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; }
    [SetUp]
    public void Setup() => StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, folder)
        => new BinaryFileService(new FileSystemOptions { RootFolder = Path.Combine(folder, TEST_FOLDER) }));
    [TearDown]
    public async Task TearDown() => await StorageTestContext.DisposeAsync();

    [Test]
    public Task Recursive_Directories() => StorageTestContext.Test_Recursive_Directories(TEST_FOLDER);
    [Test]
    public Task Recursive_Directories_Async() => StorageTestContext.Test_Recursive_DirectoriesAsync(TEST_FOLDER);
}