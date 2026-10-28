using IO.Testing.Helpers;
using Regira.IO.Extensions;
using Regira.IO.Storage.SSH;

namespace IO.Testing.SSH;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Containers")]
public class SshFileProcessorTests
{
    private SftpServer? _server;
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp() => _server = await SftpServer.StartAsync();
    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_server != null) await _server.DisposeAsync();
    }

    [SetUp]
    public async Task Setup()
    {
        StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, _)
            => new SftpService(new SftpCommunicator(_server!.Config())));
        foreach (var file in StorageTestContext.SourceFiles)
        {
            await StorageTestContext.FileService.Save(file.Identifier!, file.GetBytes()!, file.ContentType);
        }
    }
    [TearDown]
    public async Task TearDown()
    {
        // unset when the fixture was ignored
        if (StorageTestContext != null) await StorageTestContext.DisposeAsync();
    }

    // the processor hands out the files' absolute paths on the server
    [Test]
    public Task Recursive_Directories() => StorageTestContext.Test_Recursive_Directories(expected: StorageTestContext.SourceFiles.Select(file => $"{SftpServer.Root}/{file.Identifier}"));
}
