using IO.Testing.Helpers;
using Microsoft.Extensions.Configuration;
using Regira.IO.Storage.SSH;

namespace IO.Testing.SSH;

[TestFixture]
[Ignore("wait for proper config")]
[Parallelizable(ParallelScope.Self)]
public class SshStorageTests
{
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; }
    [SetUp]
    public void Setup() => StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, folder)
        =>
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddUserSecrets(GetType().Assembly, true);
        var configuration = configBuilder.Build();
        var sshSection = configuration.GetSection("Storage:SSH");
        var config = new SftpConfig
        {
            ContainerName = sshSection["ContainerName"],
            Host = sshSection["Host"],
            Port = int.Parse(sshSection["Port"]!),
            UserName = sshSection["Username"],
            Password = sshSection["Password"]
        };

        return new SftpService(new SftpCommunicator(config));
    });
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