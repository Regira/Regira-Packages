using Regira.Invoicing.Billit.Config;
using Regira.Invoicing.Billit.Models.Files;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.IO.Storage.Compression;
using System.Net.Http.Json;
using System.Xml.Linq;

namespace Regira.Invoicing.Billit.Services;

public interface IFileManager
{
    Task<FileDto?> GetFileAsync(string identifier);
    Task<bool> HasDocuments(FileDto item);
    Task<IList<IBinaryFile>> GetDocuments(FileDto item);
    Task<IBinaryFile> GetBundledDocuments(FileDto item);
}
public class FileManager(IHttpClientFactory clientFactory) : IFileManager
{
    private readonly XNamespace nsCac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private readonly XNamespace nsCbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const string Path = "/v1/files";
    private readonly HttpClient _client = clientFactory.CreateClient(BillitConstants.HttpClientName);

    public async Task<FileDto?> GetFileAsync(string identifier)
    {
        var url = $"{Path}/{identifier}";
        var item = await _client.GetFromJsonAsync<FileDto>(url);
        if (item != null)
        {
            item.HasDocuments = await HasDocuments(item);
        }
        return item;
    }

    public async Task<bool> HasDocuments(FileDto item)
    {
        using var ms = new MemoryStream(item.FileContent);
        var xdoc = await XDocument.LoadAsync(ms, LoadOptions.None, CancellationToken.None);
        return xdoc
            .Descendants(nsCac + "AdditionalDocumentReference")
            .Any();
    }

    public async Task<IList<IBinaryFile>> GetDocuments(FileDto item)
    {
        using var ms = new MemoryStream(item.FileContent);
        var xdoc = await XDocument.LoadAsync(ms, LoadOptions.None, CancellationToken.None);
        var documents = xdoc
            .Descendants(nsCac + "AdditionalDocumentReference")
            .Select(docRef =>
            {
                var embedded = docRef.Element(nsCac + "Attachment")?.Element(nsCbc + "EmbeddedDocumentBinaryObject");

                if (embedded == null)
                {
                    return null;
                }

                var fileName = (string?)embedded.Attribute("filename") ?? "unknown";
                var contentType = (string?)embedded.Attribute("mimeCode") ?? "application/octet-stream";
                var base64 = (string?)embedded ?? string.Empty;

                byte[] bytes = [];
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    try
                    {
                        bytes = Convert.FromBase64String(base64);
                    }
                    catch
                    {
                        // Handle invalid base64 or leave empty
                    }
                }

                return new BinaryFileItem
                {
                    FileName = fileName,
                    Bytes = bytes,
                    ContentType = contentType
                };
            })
            .Where(x => x?.HasContent() == true)
            .OfType<IBinaryFile>()// make not nullable
            .ToList();

        return documents;
    }

    public async Task<IBinaryFile> GetBundledDocuments(FileDto item)
    {
        var documents = await GetDocuments(item);
        if (documents.Any())
        {
            if (documents.Count > 1)
            {
                return documents.Zip().ToBinaryFile($"{item.FileName}-documents.zip");
            }
            return documents.First();
        }
        return new BinaryFileItem
        {
            FileName = item.FileName,
            Bytes = item.FileContent,
            ContentType = item.MimeType
        };
    }
}