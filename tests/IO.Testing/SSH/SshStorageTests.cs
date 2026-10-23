using IO.Testing.Helpers;
using Regira.IO.Extensions;
using Regira.IO.Storage.SSH;
using Renci.SshNet.Common;

namespace IO.Testing.SSH;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Containers")]
public class SshStorageTests
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
    public async Task Filter_Non_Recursive() => await StorageTestContext.Test_Filter_Non_Recursive();
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

    /// <summary>A configured fingerprint the server's key does not match ends the connection before anything is read.</summary>
    [Test]
    public async Task A_Server_Presenting_Another_Host_Key_Is_Refused()
    {
        var otherKey = "SHA256:" + new string('A', 43);
        using var communicator = new SftpCommunicator(_server!.Config(otherKey));
        var service = new SftpService(communicator);

        Assert.That(otherKey, Is.Not.EqualTo(_server.HostKeyFingerprint));
        await Assert.CatchAsync<SshConnectionException>(() => service.List());
    }
}
