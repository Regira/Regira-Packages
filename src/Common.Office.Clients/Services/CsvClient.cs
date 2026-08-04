using Regira.IO.Abstractions;
using Regira.Office.Clients.Abstractions;
using Regira.Office.Csv.Abstractions;
using Regira.Office.Csv.Models;

namespace Regira.Office.Clients.Services;

public class CsvClient(HttpClient client) : OfficeClientBase(client), ICsvService
{
    private const string ParsePath = "csv/parse";
    private const string ParseFilePath = "csv/parse/file";
    private const string WritePath = "csv/write";
    private const string WriteFilePath = "csv/write/file";

    public async Task<List<IDictionary<string, object>>> Read(string input, CsvOptions? options = null, CancellationToken cancellationToken = default)
        => await PostStringForJsonAsync<List<IDictionary<string, object>>>(ParsePath, input, "text/plain", cancellationToken) ?? [];

    public async Task<List<IDictionary<string, object>>> Read(IBinaryFile input, CsvOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(GetBytes(input)), "formFile", "data.csv");
        return await PostMultipartAsync<List<IDictionary<string, object>>>(ParseFilePath, content, cancellationToken) ?? [];
    }

    public async Task<string> Write(IEnumerable<IDictionary<string, object>> items, CsvOptions? options = null, CancellationToken cancellationToken = default)
        => await PostJsonForTextAsync(WritePath, items.ToList(), cancellationToken) ?? string.Empty;

    public async Task<IMemoryFile> WriteFile(IEnumerable<IDictionary<string, object>> items, CsvOptions? options = null, CancellationToken cancellationToken = default)
        => await PostJsonForFileAsync(WriteFilePath, items.ToList(), cancellationToken);

    private static byte[] GetBytes(IBinaryFile file)
        => (file as IMemoryBytesFile)?.Bytes ?? throw new ArgumentException("File must be an in-memory file with bytes.", nameof(file));
}
