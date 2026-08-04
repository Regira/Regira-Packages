using IO.Testing.Helpers;
using Microsoft.Extensions.Configuration;
using Regira.IO.Storage.SSH;

namespace IO.Testing.SSH;

[TestFixture]
[Ignore("wait for proper config")]
[Parallelizable(ParallelScope.Self)]
public class SshFileProcessorTests
{
    private const string TEST_FOLDER = "file_processor";
    public StorageTestHelper.IStorageTestContext StorageTestContext { get; set; }

    [SetUp]
    public void Setup() => StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, _)
        =>
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddUserSecrets(GetType().Assembly, true);
        var configuration = configBuilder.Build();
        var sshSection = configuration.GetSection("Storage:SSH") ??
                         throw new NullReferenceException("Section Storage:SSH is missing");
        var config = new SftpConfig
        {
            ContainerName = sshSection["ContainerName"],
            Host = sshSection["Host"]!,
            Port = int.Parse(sshSection["Port"]!),
            UserName = sshSection["Username"]!,
            Password = sshSection["Password"]
        };

        return new SftpService(new SftpCommunicator(config));
    });
    [TearDown]
    public async Task TearDown() => await StorageTestContext.DisposeAsync();

    [Test]
    public Task Recursive_Directories() => StorageTestContext.Test_Recursive_Directories(TEST_FOLDER);
    [Test]
    public Task Recursive_Directories_Async() => StorageTestContext.Test_Recursive_DirectoriesAsync(TEST_FOLDER);
}