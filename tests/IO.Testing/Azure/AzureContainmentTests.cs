using Azure;
using Azure.Storage.Blobs;
using IO.Testing.Helpers;
using Regira.IO.Storage.Azure;

namespace IO.Testing.Azure;

/// <summary>
/// The blob an identifier names, read off the client the Azure SDK builds for it. The communicator is marked open, so no
/// request reaches a storage account; a move sends its copy to a local stand-in.
/// </summary>
[TestFixture]
public class AzureContainmentTests
{
    private const string ConnectionString = "UseDevelopmentStorage=true";
    private const string ContainerName = "uploads";

    private static BinaryBlobService CreateService(bool contained = true)
    {
        var communicator = new AzureCommunicator(new AzureOptions { ConnectionString = ConnectionString, ContainerName = ContainerName, Contained = contained })
        {
            Container = new BlobContainerClient(ConnectionString, ContainerName),
            IsOpened = true
        };
        return new BinaryBlobService(communicator);
    }

    [TestCase("invoices/inv-001.pdf", "invoices/inv-001.pdf")]
    [TestCase(@"invoices\inv-001.pdf", "invoices/inv-001.pdf")]
    [TestCase("/invoices/inv-001.pdf", "invoices/inv-001.pdf")]
    [TestCase("invoices/../inv-001.pdf", "inv-001.pdf")]
    [TestCase("inv..001.pdf", "inv..001.pdf")]
    [TestCase("inv 001.pdf", "inv 001.pdf")]
    [TestCase("factuur-ü/inv-001.pdf", "factuur-ü/inv-001.pdf")]
    [TestCase("%2e%2e%2fother%2finv-001.pdf", "%2e%2e%2fother%2finv-001.pdf", Description = "the SDK escapes the %: a name, not a path")]
    public void An_Identifier_Names_A_Blob_In_The_Container(string identifier, string expected)
    {
        var blob = CreateService().GetBlobReference(identifier);

        Assert.Multiple(() =>
        {
            Assert.That(blob.BlobContainerName, Is.EqualTo(ContainerName));
            Assert.That(blob.Name, Is.EqualTo(expected));
        });
    }

    [TestCase("../other/inv-001.pdf")]
    [TestCase(@"..\other\inv-001.pdf")]
    [TestCase("invoices/../../other/inv-001.pdf")]
    [TestCase("../../inv-001.pdf")]
    [TestCase("..\0/inv-001.pdf", Description = "a server that ends the path at the null character reads ..")]
    public async Task An_Identifier_Leaving_The_Container_Is_Refused(string identifier)
    {
        var service = CreateService();

        Assert.Throws<UnauthorizedAccessException>(() => service.GetBlobReference(identifier));
        Assert.Throws<UnauthorizedAccessException>(() => service.GetAbsoluteUri(identifier));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Exists(identifier));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBytes(identifier));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetInfo(identifier));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Save(identifier, [1]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Delete(identifier));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Move(identifier, "inv-001.pdf"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Move("inv-001.pdf", identifier));
    }

    [TestCase("%2e%2e/other/inv-001.pdf")]
    [TestCase(".%2E/other/inv-001.pdf")]
    public void An_Address_Is_Refused_When_A_Url_Reads_It_Out_Of_The_Container(string identifier)
    {
        var service = CreateService();

        // the SDK escapes the %, so the blob it names stays in the container; GetAbsoluteUri spells the address as it
        // stands, and a URL reads %2e as a dot
        Assert.That(service.GetBlobReference(identifier).BlobContainerName, Is.EqualTo(ContainerName));
        Assert.Throws<UnauthorizedAccessException>(() => service.GetAbsoluteUri(identifier));
        Assert.Throws<UnauthorizedAccessException>(() => service.GetBlobUri(identifier));
    }

    [TestCase("inv-001?draft.pdf", "inv-001%3Fdraft.pdf")]
    [TestCase("inv-001#2.pdf", "inv-001%232.pdf")]
    [TestCase("inv-001%2e.pdf", "inv-001%252e.pdf")]
    public async Task Move_Copies_The_Blob_The_Source_Names(string source, string expected)
    {
        await using var server = new RecordingHttpServer();
        var containerUri = new Uri(server.BaseUri, $"devstoreaccount1/{ContainerName}/");
        var service = new BinaryBlobService(new AzureCommunicator(new AzureOptions { ConnectionString = ConnectionString, ContainerName = ContainerName })
        {
            Container = new BlobContainerClient(new Uri(containerUri.AbsoluteUri.TrimEnd('/'))),
            IsOpened = true
        });

        await Assert.ThrowsAsync<RequestFailedException>(() => service.Move(source, "inv-001.pdf"), "the stand-in refuses the copy, so the source stays");

        Assert.That(server.Requests.Single().Headers["x-ms-copy-source"], Is.EqualTo(new Uri(containerUri, expected).AbsoluteUri));
    }

    [Test]
    public void Contained_False_Leaves_The_Address_To_The_Sdk()
    {
        // the SDK resolves the dot segments of the address it builds: ../ names a blob in another container
        var service = CreateService(contained: false);

        var blob = service.GetBlobReference("../other/inv-001.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(blob.BlobContainerName, Is.EqualTo("other"));
            Assert.That(service.GetBlobUri("%2e%2e/other/inv-001.pdf").AbsolutePath, Is.EqualTo("/devstoreaccount1/other/inv-001.pdf"));
        });
    }

    [Test]
    public async Task The_Synchronous_Members_Make_No_Request()
    {
        // an endpoint nothing listens on: a request would fail, or wait out the SDK's retries
        const string unreachable = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;";
        var service = new BinaryBlobService(new AzureCommunicator(new AzureOptions { ConnectionString = unreachable, ContainerName = ContainerName }));

        var (uri, identifier, folder) = await Task.Run(() => (
            service.GetAbsoluteUri("invoices/inv-001.pdf"),
            service.GetIdentifier("http://127.0.0.1:1/devstoreaccount1/uploads/invoices/inv-001.pdf"),
            service.GetRelativeFolder("invoices/inv-001.pdf"))).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(uri, Is.EqualTo("http://127.0.0.1:1/devstoreaccount1/uploads/invoices/inv-001.pdf"));
        Assert.That(identifier, Is.EqualTo("invoices/inv-001.pdf"));
        Assert.That(folder, Is.EqualTo("invoices"));
    }
}
