using IO.Testing.Helpers;
using Regira.IO.Storage;
using Regira.IO.Storage.GitHub;
using Regira.Serializing.Newtonsoft.Json;

namespace IO.Testing.GitHub;

/// <summary>
/// The addresses the GitHub backend requests for an identifier, read off a local server that stands in for the API.
/// </summary>
[TestFixture]
public class GitHubContainmentTests
{
    private const string Token = "test-token";
    private const string ContentPath = "/repos/owner/repo/contents/files/";
    private RecordingHttpServer _server = null!;

    // a stored file's metadata: Save and Delete find its sha and go on to write
    [SetUp]
    public void SetUp() => _server = new RecordingHttpServer("""{ "sha": "3d21ec53a331a6f037a91c368710b99387d012c1" }""");

    [TearDown]
    public async Task TearDown() => await _server.DisposeAsync();

    private GitHubCommunicator CreateCommunicator(bool contained = true) => new(new GitHubOptions
    {
        Uri = new Uri(_server.BaseUri, "repos/owner/repo").ToString(),
        Key = Token,
        ContentPath = "files",
        Contained = contained
    });

    [TestCase("invoices/inv-001.pdf", "invoices/inv-001.pdf")]
    [TestCase(@"invoices\inv-001.pdf", "invoices/inv-001.pdf")]
    [TestCase("/invoices/inv-001.pdf", "invoices/inv-001.pdf")]
    [TestCase("invoices/../inv-001.pdf", "inv-001.pdf")]
    [TestCase("inv..001.pdf", "inv..001.pdf")]
    [TestCase("inv 001.pdf", "inv%20001.pdf")]
    [TestCase("factuur-ü/inv-001.pdf", "factuur-%C3%BC/inv-001.pdf")]
    [TestCase("inv-001#2.pdf", "inv-001%232.pdf", Description = "a # is part of the name, not a fragment")]
    public async Task Every_Request_Goes_Below_The_Content_Path(string identifier, string expected)
    {
        using var communicator = CreateCommunicator();
        var service = new GitHubService(communicator, new JsonSerializer());

        await service.Exists(identifier);
        await service.GetBytes(identifier);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Save(identifier, [1]), "the stand-in refuses the write");
        await service.Delete(identifier);

        Assert.That(_server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[]
        {
            $"GET {ContentPath}{expected}",
            $"GET {ContentPath}{expected}",
            $"GET {ContentPath}{expected}",
            $"PUT {ContentPath}{expected}",
            $"GET {ContentPath}{expected}",
            $"DELETE {ContentPath}{expected}"
        }));
    }

    [Test]
    public async Task A_Branch_Named_In_The_Identifier_Is_Read_From_And_Written_To()
    {
        using var communicator = CreateCommunicator();
        var service = new GitHubService(communicator, new JsonSerializer());

        await service.GetBytes("inv-001.pdf?ref=archive");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Save("inv-001.pdf?ref=archive", [1]));

        // the write names its branch in the body
        Assert.That(_server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[]
        {
            $"GET {ContentPath}inv-001.pdf?ref=archive",
            $"GET {ContentPath}inv-001.pdf?ref=archive",
            $"PUT {ContentPath}inv-001.pdf"
        }));
    }

    [TestCase("../other/inv-001.pdf")]
    [TestCase(@"..\other\inv-001.pdf")]
    [TestCase("invoices/../../other/inv-001.pdf")]
    [TestCase("../../../../../user")]
    [TestCase("%2e%2e/other/inv-001.pdf", Description = "a URL reads %2e as a dot")]
    [TestCase(".%2E/other/inv-001.pdf")]
    [TestCase("..%2Fother%2Finv-001.pdf", Description = "a server that decodes %2F reads ../other")]
    [TestCase("..\0/inv-001.pdf", Description = "a server that ends the path at the null character reads ..")]
    public async Task An_Identifier_Leaving_The_Content_Path_Is_Refused(string identifier)
    {
        using var communicator = CreateCommunicator();
        var service = new GitHubService(communicator, new JsonSerializer());

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<UnauthorizedAccessException>(() => service.GetAbsoluteUri(identifier));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Exists(identifier));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBytes(identifier));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Save(identifier, [1]));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Delete(identifier));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Move(identifier, "inv-001.pdf"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.List(new FileSearchObject { FolderUri = identifier }));
            Assert.That(_server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.Empty);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task The_Token_Goes_To_The_Configured_Host_Only(bool contained)
    {
        await using var elsewhere = new RecordingHttpServer();
        using var communicator = CreateCommunicator(contained);
        var service = new GitHubService(communicator, new JsonSerializer());
        var identifier = new Uri(elsewhere.BaseUri, "collect").ToString();

        await Assert.ThrowsAsync<HttpRequestException>(() => service.Save(identifier, [1]), "the stand-in refuses the write");
        await service.Delete(identifier);

        Assert.That(elsewhere.Requests.Select(r => $"{r.Method} {r.Target} {r.Authorization}"), Is.Empty);
    }

    [Test]
    public async Task Contained_False_Requests_The_Address_The_Dot_Segments_Resolve_To()
    {
        using var communicator = CreateCommunicator(contained: false);
        var service = new GitHubService(communicator, new JsonSerializer());

        await service.GetBytes("../other/inv-001.pdf");

        Assert.That(_server.Requests.Select(r => $"{r.Method} {r.Target}"), Is.EqualTo(new[] { "GET /repos/owner/repo/contents/other/inv-001.pdf" }));
    }

    [Test]
    public async Task The_Own_Client_Sends_The_Same_Headers()
    {
        using var communicator = CreateCommunicator();
        var service = new GitHubService(communicator, new JsonSerializer());

        await service.GetBytes("inv-001.pdf");

        var request = _server.Requests.Single();
        Assert.That($"{request.Headers["Authorization"]} {request.Headers["Cache-Control"]}", Is.EqualTo($"Bearer {Token} no-cache"));
        Assert.That(request.Headers["User-Agent"], Is.EqualTo("Regira.IO.Storage.GitHub"));
    }

    [Test]
    public async Task A_Cookie_One_Communicator_Receives_Is_Not_Sent_By_Another()
    {
        await using var server = new RecordingHttpServer("""{ "sha": "3d21ec53a331a6f037a91c368710b99387d012c1" }""", setCookie: "session=tenant-a; Path=/");
        GitHubCommunicator Create(string key) => new(new GitHubOptions { Uri = new Uri(server.BaseUri, "repos/owner/repo").ToString(), Key = key });
        using var tenantA = Create("token-a");
        using var tenantB = Create("token-b");

        await new GitHubService(tenantA, new JsonSerializer()).Exists("inv-001.pdf");
        await new GitHubService(tenantA, new JsonSerializer()).Exists("inv-001.pdf");
        await new GitHubService(tenantB, new JsonSerializer()).Exists("inv-001.pdf");

        Assert.That(server.Requests.Select(r => r.Headers.GetValueOrDefault("Cookie")), Is.All.Null, "the shared pool keeps no cookies");
    }
}
