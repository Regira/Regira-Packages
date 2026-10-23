using IO.Testing.Helpers;
using Regira.IO.Storage.FileSystem;

namespace IO.Testing.FileSystem;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class FsFileProcessorTests
{
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; }
    // rooted where the context creates its files
    [SetUp]
    public void Setup() => StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, folder)
        => new BinaryFileService(new FileSystemOptions { RootFolder = folder }));
    [TearDown]
    public async Task TearDown() => await StorageTestContext.DisposeAsync();

    [Test]
    public Task Recursive_Directories() => StorageTestContext.Test_Recursive_Directories(expected: StorageTestContext.SourceFiles.Select(file => file.Path!));
}