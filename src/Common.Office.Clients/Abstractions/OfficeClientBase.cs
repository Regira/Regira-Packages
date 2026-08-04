using Regira.IO.Abstractions;
using Regira.Media.Drawing.Models;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Regira.Office.Clients.Abstractions;

public abstract class OfficeClientBase(HttpClient client)
{
    protected HttpClient Client => client;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    protected async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    protected async Task<T?> PostJsonAsync<TBody, T>(string url, TBody body, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    protected async Task<ImageFile> PostJsonForFileAsync<TBody>(string url, TBody body, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
        return new ImageFile
        {
            Bytes = await ReadBytesAsync(response, cancellationToken),
            ContentType = response.Content.Headers.ContentType?.MediaType
        };
    }

    protected async Task<T?> PostMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync(url, content, cancellationToken);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    protected async Task<ImageFile> PostMultipartForFileAsync(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync(url, content, cancellationToken);
        await EnsureSuccess(response);
        return new ImageFile
        {
            Bytes = await ReadBytesAsync(response, cancellationToken),
            ContentType = response.Content.Headers.ContentType?.MediaType
        };
    }

    protected async Task<string?> PostMultipartForTextAsync(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync(url, content, cancellationToken);
        await EnsureSuccess(response);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != null && mediaType.Contains("json"))
            return await response.Content.ReadFromJsonAsync<string?>(JsonOptions, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    protected async Task<IList<IMemoryFile>> PostMultipartForFilesAsync(string url, MultipartFormDataContent content, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsync(url, content, cancellationToken);
        await EnsureSuccess(response);
        var bytes = await ReadBytesAsync(response, cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is "application/zip" or "application/octet-stream" || IsZip(bytes))
            return UnzipFiles(bytes);
        return [new ImageFile { Bytes = bytes, ContentType = mediaType }];
    }

    protected async Task<string?> PostJsonForTextAsync<TBody>(string url, TBody body, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    protected async Task<T?> PostStringForJsonAsync<T>(string url, string text, string mediaType = "text/plain", CancellationToken cancellationToken = default)
    {
        using var body = new StringContent(text, Encoding.UTF8, mediaType);
        var response = await client.PostAsync(url, body, cancellationToken);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    protected static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{response.RequestMessage?.RequestUri} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
    }

    private static async Task<byte[]> ReadBytesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, 81920, cancellationToken);
        return ms.ToArray();
    }

    private static bool IsZip(byte[] bytes) => bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;

    private static IList<IMemoryFile> UnzipFiles(byte[] zipBytes)
    {
        var result = new List<IMemoryFile>();
        using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            result.Add(new ImageFile { Bytes = ms.ToArray() });
        }
        return result;
    }
}
