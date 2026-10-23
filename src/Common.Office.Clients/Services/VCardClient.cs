using Regira.Office.Clients.Abstractions;
using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Exceptions;
using Regira.Office.VCards.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Regira.Office.Clients.Services;

public class VCardClient(HttpClient client) : OfficeClientBase(client), IVCardService
{
    private const string ReadPath = "vcard/read";
    private const string WritePath = "vcard/write";

    /// <exception cref="InvalidCardException"><paramref name="content"/> holds no contact the API can read</exception>
    public async Task<VCard> Read(string content, CancellationToken cancellationToken = default)
    {
        var items = await ReadMany(content, cancellationToken);
        return items.FirstOrDefault() ?? throw new InvalidCardException("Invalid card", content);
    }

    /// <exception cref="InvalidCardException"><paramref name="content"/> holds no contact the API can read</exception>
    public async Task<IEnumerable<VCard>> ReadMany(string content, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "vcfFile", "contacts.vcf");
        var response = await Client.PostAsync(ReadPath, form, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidCardException(string.IsNullOrWhiteSpace(message) ? "Invalid card" : message.Trim('"'), content);
        }
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<List<VCard>>(JsonOptions, cancellationToken) ?? [];
    }

    public Task<string> Write(VCard item, VCardVersion version = VCardVersion.V3_0, CancellationToken cancellationToken = default)
        => Write([item], version, cancellationToken);

    public async Task<string> Write(IEnumerable<VCard> items, VCardVersion version = VCardVersion.V3_0, CancellationToken cancellationToken = default)
        => await PostJsonForTextAsync($"{WritePath}?version={version}", items.ToArray(), cancellationToken) ?? string.Empty;
}
