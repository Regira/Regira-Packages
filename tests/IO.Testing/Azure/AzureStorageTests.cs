using System.Text;
using Azure.Storage.Blobs;
using IO.Testing.Helpers;
using Microsoft.Extensions.Configuration;
using Regira.IO.Extensions;
using Regira.IO.Storage.Azure;

namespace IO.Testing.Azure;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Network")]
public class AzureStorageTests
{
    private const string ContainerName = "test-container";
    public StorageTestHelper.StorageTestContext<BinaryBlobService> StorageTestContext { get; set; }
    private string? ConnectionString { get; set; }
    [SetUp]
    public async Task Setup()
    {
        StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, _) =>
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddUserSecrets(typeof(AzureStorageTests).Assembly, true);
            var configuration = configBuilder.Build();
            ConnectionString = configuration["Storage:Azure:ConnectionString"];
            var cf = new AzureOptions
            {
                ConnectionString = ConnectionString,
                ContainerName = ContainerName
            };
            var cm = new AzureCommunicator(cf);
            return new BinaryBlobService(cm);
        });
        // create initial files on Azure
        foreach (var file in StorageTestContext.SourceFiles)
        {
            await StorageTestContext.FileService.Save(file.Identifier!, file.GetBytes()!, file.ContentType);
        }
    }

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

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_Stores_The_Given_Content_Type(bool asStream)
    {
        var identifier = "dir3/report";
        var bytes = Encoding.UTF8.GetBytes("{\"title\":\"report\"}");

        var saved = await Save(identifier, bytes, "application/json", asStream);

        var properties = await GetProperties(saved);
        Assert.Multiple(() =>
        {
            Assert.That(properties.ContentType, Is.EqualTo("application/json"));
            Assert.That(properties.ContentEncoding, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_Derives_The_Content_Type_From_The_Identifier(bool asStream)
    {
        var identifier = "dir3/notes.txt";
        // a UTF-8 byte-order mark: a character set is still not a content coding
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("notes")).ToArray();

        var saved = await Save(identifier, bytes, null, asStream);

        var properties = await GetProperties(saved);
        Assert.Multiple(() =>
        {
            Assert.That(properties.ContentType, Is.EqualTo("text/plain"));
            Assert.That(properties.ContentEncoding, Is.Null);
        });
    }

    /// <returns>The identifier <c>Save</c> stored the blob under: its name in the container.</returns>
    private async Task<string> Save(string identifier, byte[] bytes, string? contentType, bool asStream)
    {
        if (asStream)
        {
            using var stream = new MemoryStream(bytes);
            return await StorageTestContext.FileService.Save(identifier, stream, contentType);
        }
        return await StorageTestContext.FileService.Save(identifier, bytes, contentType);
    }

    private async Task<global::Azure.Storage.Blobs.Models.BlobProperties> GetProperties(string blobName)
    {
        var blob = new BlobContainerClient(ConnectionString, ContainerName).GetBlobClient(blobName);
        return (await blob.GetPropertiesAsync()).Value;
    }
}