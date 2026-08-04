using IO.Testing.Helpers;
using Microsoft.Extensions.Configuration;
using Regira.IO.Extensions;
using Regira.IO.Storage.Azure;

namespace IO.Testing.Azure;

[TestFixture]
[Parallelizable(ParallelScope.Fixtures)]
public class AzureFileProcessorTests
{
    private const string TEST_FOLDER = "file_processor";
    public StorageTestHelper.StorageTestContext<BinaryBlobService> StorageTestContext { get; set; }
    [SetUp]
    public async Task Setup()
    {
        StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, _) =>
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddUserSecrets(typeof(AzureStorageTests).Assembly, true);
            var configuration = configBuilder.Build();
            var azureConnectionString = configuration["Storage:Azure:ConnectionString"];
            var cf = new AzureOptions
            {
                ConnectionString = azureConnectionString,
                ContainerName = "test-container"
            };
            var cm = new AzureCommunicator(cf);
            return new BinaryBlobService(cm);
        });
        // create initial files on Azure
        foreach (var file in StorageTestContext.SourceFiles)
        {
            await StorageTestContext.FileService.Save($"{TEST_FOLDER}/{file.Identifier}", file.GetBytes()!, file.ContentType);
        }
    }

    [TearDown]
    public async Task TearDown() => await StorageTestContext.DisposeAsync();

    [Test]
    public Task Recursive_Directories() => StorageTestContext.Test_Recursive_Directories(TEST_FOLDER);
    [Test]
    public Task Recursive_Directories_Async() => StorageTestContext.Test_Recursive_DirectoriesAsync(TEST_FOLDER);
}