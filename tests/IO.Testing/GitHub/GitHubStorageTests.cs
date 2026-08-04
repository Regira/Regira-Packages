using IO.Testing.Helpers;
using Microsoft.Extensions.Configuration;
using Regira.IO.Extensions;
using Regira.IO.Storage.GitHub;
using Regira.Serializing.Newtonsoft.Json;
using System.Net;
using System.Text;
namespace IO.Testing.GitHub;

[TestFixture]
public class GitHubStorageTests
{
    public StorageTestHelper.StorageTestContext<GitHubService> StorageTestContext { get; set; } = null!;
    private GitHubCommunicator _communicator = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(GitHubStorageTests).Assembly, true)
            .Build();

        _communicator = new GitHubCommunicator(new GitHubOptions
        {
            Uri = config["Storage:GitHub:Uri"]!,
            Key = config["Storage:GitHub:Key"],
            Branch = config["Storage:GitHub:Branch"] ?? "main",
            ContentPath = $"test-write/{Guid.NewGuid():D}"
        });

        StorageTestContext = StorageTestHelper.CreateDecoratedFileService((_, _) =>
            new GitHubService(_communicator, new JsonSerializer()));

        foreach (var file in StorageTestContext.SourceFiles)
        {
            await StorageTestContext.FileService.Save(file.Identifier!, file.GetBytes()!, file.ContentType);
        }
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        try
        {
            await StorageTestContext.DisposeAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests or HttpStatusCode.NotFound)
        {
            // GitHub secondary rate limit hit during remote cleanup.
            // Local folder and file handles were already released before the remote call threw.
            // The unique GUID test folder is orphaned in the repo but is harmless.
        }
        finally
        {
            _communicator.Dispose();
        }
    }

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
    public async Task Add_File()
    {
        const string id = "temp-write/file-add.txt";
        var bytes = Encoding.UTF8.GetBytes("add-test");
        var uri = await StorageTestContext.FileService.Save(id, bytes);
        Assert.That(uri, Is.Not.Empty);
        byte[]? saved = null;
        await StorageTestHelper.Eventually(async () =>
            (saved = await StorageTestContext.FileService.GetBytes(id)) != null && saved.SequenceEqual(bytes));
        Assert.That(saved, Is.EquivalentTo(bytes));
        await StorageTestContext.FileService.Delete(id);
    }
    [Test]
    public async Task Update_File()
    {
        const string id = "temp-write/file-update.txt";
        var original = Encoding.UTF8.GetBytes("original");
        await StorageTestContext.FileService.Save(id, original);
        var updated = Encoding.UTF8.GetBytes("updated");
        await StorageTestContext.FileService.Save(id, updated);
        byte[]? saved = null;
        await StorageTestHelper.Eventually(async () =>
            (saved = await StorageTestContext.FileService.GetBytes(id)) != null && saved.SequenceEqual(updated));
        Assert.That(saved, Is.Not.EquivalentTo(original));
        Assert.That(saved, Is.EquivalentTo(updated));
        await StorageTestContext.FileService.Delete(id);
    }
    [Test]
    public async Task Remove_File()
    {
        const string id = "temp-write/file-remove.txt";
        await StorageTestContext.FileService.Save(id, Encoding.UTF8.GetBytes("remove-test"));
        var created = await StorageTestHelper.Eventually(() => StorageTestContext.FileService.Exists(id));
        Assert.That(created, Is.True);
        await StorageTestContext.FileService.Delete(id);
        var removed = await StorageTestHelper.Eventually(async () => !await StorageTestContext.FileService.Exists(id));
        Assert.That(removed, Is.True);
    }
}
