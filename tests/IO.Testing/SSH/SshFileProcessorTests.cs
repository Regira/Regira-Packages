using IO.Testing.Helpers;
using Regira.IO.Storage.SSH;

namespace IO.Testing.SSH;

[TestFixture]
[Ignore("wait for proper config")]
[Parallelizable(ParallelScope.Self)]
[Category("Network")]
public class SshFileProcessorTests
{
    private const string TEST_FOLDER = "file_processor";
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; } = null!;

    [SetUp]
    public void Setup()
    {
        var config = TestSecrets.Ssh();
        StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, _)
            => new SftpService(new SftpCommunicator(config)));
    }
    [TearDown]
    public async Task TearDown()
    {
        // unset when Setup ignored the fixture
        if (StorageTestContext != null) await StorageTestContext.DisposeAsync();
    }

    [Test]
    public Task Recursive_Directories() => StorageTestContext.Test_Recursive_Directories(TEST_FOLDER);
    [Test]
    public Task Recursive_Directories_Async() => StorageTestContext.Test_Recursive_DirectoriesAsync(TEST_FOLDER);
}