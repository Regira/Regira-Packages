using IO.Testing.Helpers;
using Regira.IO.Storage;
using Regira.IO.Storage.GitHub;
using Regira.Serializing.Newtonsoft.Json;

namespace IO.Testing.GitHub;

/// <summary>
/// <see cref="GitHubService.GetInfo"/> reads the size off the contents API's answer for the path, given here by a local
/// server that stands in for the API. That answer carries no time.
/// </summary>
[TestFixture]
public class GitHubInfoTests
{
    private static async Task<StoredFileInfo?> GetInfo(RecordingHttpServer server, string identifier)
    {
        using var communicator = new GitHubCommunicator(new GitHubOptions
        {
            Uri = new Uri(server.BaseUri, "repos/owner/repo").ToString(),
            Key = "test-token",
            ContentPath = "files"
        });
        return await new GitHubService(communicator, new JsonSerializer()).GetInfo(identifier);
    }

    [Test]
    public async Task A_File_Answers_Its_Size()
    {
        await using var server = new RecordingHttpServer("""{ "type": "file", "name": "report.pdf", "path": "files/report.pdf", "size": 12345, "sha": "3d21ec53a331a6f037a91c368710b99387d012c1", "content": "", "encoding": "none" }""");

        Assert.That(await GetInfo(server, "report.pdf"), Is.EqualTo(new StoredFileInfo(12345, null)));
    }

    [Test]
    public async Task A_Folder_Answers_Null()
    {
        await using var server = new RecordingHttpServer("""[ { "type": "file", "name": "report.pdf", "path": "files/invoices/report.pdf", "size": 12345 } ]""");

        Assert.That(await GetInfo(server, "invoices"), Is.Null);
    }

    [Test]
    public async Task A_Missing_File_Answers_Null()
    {
        await using var server = new RecordingHttpServer();

        Assert.That(await GetInfo(server, "missing.pdf"), Is.Null);
    }
}
