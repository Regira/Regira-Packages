using System.Net;
using System.Net.Http.Headers;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Regira.IO.Storage;
using Regira.IO.Storage.Azure;

namespace IO.Testing.Azure;

/// <summary>
/// <see cref="BinaryBlobService.GetInfo"/> reads a blob's properties, answered here through a stand-in transport, so no
/// request reaches a storage account.
/// </summary>
[TestFixture]
public class AzureInfoTests
{
    private const string ConnectionString = "UseDevelopmentStorage=true";

    private static BinaryBlobService CreateService(HttpResponseMessage answer)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(new StubHandler(answer))) };
        return new BinaryBlobService(new AzureCommunicator(new AzureOptions { ConnectionString = ConnectionString, ContainerName = "uploads" })
        {
            Container = new BlobContainerClient(ConnectionString, "uploads", options),
            IsOpened = true
        });
    }

    private static HttpResponseMessage Properties(long length, string? isFolder = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        response.Content.Headers.ContentLength = length;
        response.Content.Headers.LastModified = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        response.Headers.ETag = new EntityTagHeaderValue("\"0x8DE0000000000\"");
        response.Headers.Add("x-ms-blob-type", "BlockBlob");
        if (isFolder != null)
        {
            response.Headers.Add("x-ms-meta-hdi_isfolder", isFolder);
        }
        return response;
    }

    [Test]
    public async Task A_Blob_Answers_Its_Size_And_Time()
    {
        var info = await CreateService(Properties(12345)).GetInfo("invoices/inv-001.pdf");

        Assert.That(info, Is.EqualTo(new StoredFileInfo(12345, new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc))));
        Assert.That(info!.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    // a folder on a hierarchical-namespace (Data Lake) account is a blob of its own, of length 0, marked in its metadata
    [Test]
    public async Task A_Folder_Of_A_Hierarchical_Namespace_Answers_Null()
    {
        Assert.That(await CreateService(Properties(0, "true")).GetInfo("invoices"), Is.Null);
    }

    private sealed class StubHandler(HttpResponseMessage answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(answer);
    }
}
