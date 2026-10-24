using IO.Testing.Helpers;
using Regira.IO.Storage.GitHub;
using Regira.IO.Utilities;
using Regira.Serializing.Newtonsoft.Json;
using System.Text;

namespace IO.Testing.GitHub;

/// <summary>
/// How the GitHub backend reads a file's content, from a local server that stands in for the API.
/// </summary>
[TestFixture]
public class GitHubReadTests
{
    private const string ContentPath = "/repos/owner/repo/contents/files/";
    private const string BlobsPath = "/repos/owner/repo/git/blobs/";
    private const string Sha = "3d21ec53a331a6f037a91c368710b99387d012c1";

    private static GitHubCommunicator CreateCommunicator(RecordingHttpServer server) => new(new GitHubOptions
    {
        Uri = new Uri(server.BaseUri, "repos/owner/repo").ToString(),
        Key = "test-token",
        ContentPath = "files"
    });

    // the contents API answers a file over 1 MB without its content: "content" is empty and "encoding" is "none"
    private static byte[] LargeFileJson(int size, string sha, string downloadUrl) => Encoding.UTF8.GetBytes($$"""
        { "type": "file", "name": "inv-001.pdf", "path": "files/inv-001.pdf", "size": {{size}}, "sha": "{{sha}}",
          "content": "", "encoding": "none", "download_url": "{{downloadUrl}}" }
        """);

    [TestCase(nameof(GitHubService.GetBytes))]
    [TestCase(nameof(GitHubService.GetStream))]
    public async Task A_File_Over_1_MB_Is_Read_From_Its_Blob(string read)
    {
        var bytes = new byte[1_500_000];
        new Random(1).NextBytes(bytes);
        await using var elsewhere = new RecordingHttpServer(_ => ("application/octet-stream", bytes));
        var json = LargeFileJson(bytes.Length, Sha, new Uri(elsewhere.BaseUri, "inv-001.pdf").ToString());
        await using var server = new RecordingHttpServer(r => r.Target switch
        {
            ContentPath + "inv-001.pdf" => ("application/json", json),
            BlobsPath + Sha => ("application/octet-stream", bytes),
            _ => null
        });
        using var communicator = CreateCommunicator(server);
        var service = new GitHubService(communicator, new JsonSerializer());

        var content = read == nameof(GitHubService.GetBytes)
            ? await service.GetBytes("inv-001.pdf")
            : (await service.GetStream("inv-001.pdf")).GetBytes();

        Assert.That(content, Is.EqualTo(bytes));
        Assert.That(server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[]
        {
            $"GET {ContentPath}inv-001.pdf",
            $"GET {BlobsPath}{Sha}"
        }));
        var blob = server.Requests.Last();
        Assert.That($"{blob.Headers["Accept"]} {blob.Authorization}", Is.EqualTo("application/vnd.github.raw+json Bearer test-token"));
        Assert.That(elsewhere.Requests, Is.Empty, "the download_url is on another host, where the token does not go");
    }

    [TestCase("aGVsbG8s\nIHdvcmxk\n", 12, "hello, world", Description = "the content comes base64-encoded, its lines broken")]
    [TestCase("", 0, "", Description = "an empty file")]
    public async Task A_File_Up_To_1_MB_Is_Read_In_One_Request(string base64, int size, string expected)
    {
        var json = Encoding.UTF8.GetBytes($$"""
            { "type": "file", "name": "inv-001.txt", "size": {{size}}, "sha": "{{Sha}}", "content": "{{base64.Replace("\n", "\\n")}}", "encoding": "base64" }
            """);
        await using var server = new RecordingHttpServer(_ => ("application/json", json));
        using var communicator = CreateCommunicator(server);
        var service = new GitHubService(communicator, new JsonSerializer());

        var content = await service.GetBytes("inv-001.txt");

        Assert.That(Encoding.UTF8.GetString(content!), Is.EqualTo(expected));
        Assert.That(server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[] { $"GET {ContentPath}inv-001.txt" }));
    }

    [TestCase("../../../../user", Description = "a sha is a hash; anything else would steer the address")]
    [TestCase("", Description = "no sha to read the content by")]
    public async Task A_Large_File_Without_A_Valid_Sha_Reads_As_Null_Without_A_Second_Request(string sha)
    {
        var json = LargeFileJson(1_500_000, sha, "");
        await using var server = new RecordingHttpServer(_ => ("application/json", json));
        using var communicator = CreateCommunicator(server);
        var service = new GitHubService(communicator, new JsonSerializer());

        var content = await service.GetBytes("inv-001.pdf");

        Assert.That(content, Is.Null);
        Assert.That(server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[] { $"GET {ContentPath}inv-001.pdf" }));
    }

    [Test]
    public async Task A_Blob_The_Api_Refuses_Reads_As_Null()
    {
        var json = LargeFileJson(1_500_000, Sha, "");
        await using var server = new RecordingHttpServer(r => r.Target.StartsWith(ContentPath) ? ("application/json", json) : null);
        using var communicator = CreateCommunicator(server);
        var service = new GitHubService(communicator, new JsonSerializer());

        var content = await service.GetBytes("inv-001.pdf");

        Assert.That(content, Is.Null, "as a refused contents answer does");
        Assert.That(server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[] { $"GET {ContentPath}inv-001.pdf", $"GET {BlobsPath}{Sha}" }));
    }
}
