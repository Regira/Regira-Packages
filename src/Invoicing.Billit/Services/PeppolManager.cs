using Regira.Invoicing.Billit.Config;
using Regira.Invoicing.Billit.Models.Peppol;
using Regira.Serializing.Abstractions;

namespace Regira.Invoicing.Billit.Services;

public interface IPeppolManager
{
    Task<IList<InboxItemDto>> List(string? company);
    IAsyncEnumerable<PeppolItem> EnrichList(IList<InboxItemDto> items);
}
public class PeppolManager(IHttpClientFactory clientFactory, IPartyManager partyManager, IFileManager fileManager, ISerializer serializer) : IPeppolManager
{
    private const string Path = "/v1/peppol";
    private readonly HttpClient _client = clientFactory.CreateClient(BillitConstants.HttpClientName);

    public async Task<IList<InboxItemDto>> List(string? company)
    {
        var url = $"{Path}/inbox";
        if (!string.IsNullOrWhiteSpace(company))
        {
            url = $"{url}?ReceiverCompanyID={company}";
        }

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var item = serializer.Deserialize<InboxResponse>(json);
        return item?.InboxItems ?? [];
    }

    public async IAsyncEnumerable<PeppolItem> EnrichList(IList<InboxItemDto> items)
    {
        var parties = new Dictionary<string, PeppolSender?>();
        foreach (var item in items)
        {
            if (!parties.TryGetValue(item.SenderPeppolID, out var sender))
            {
                sender = await GetParticipant(item.SenderPeppolID);
                parties.TryAdd(item.SenderPeppolID, sender);
            }

            var file = await fileManager.GetFileAsync(item.PeppolFileID);

            yield return new PeppolItem
            {
                InboxItemId = item.InboxItemID,
                CreationDate = item.CreationDate,
                SenderPeppolId = item.SenderPeppolID,
                PeppolDocumentType = item.PeppolDocumentType,
                PeppolFileId = item.PeppolFileID,
                ReceiverCompanyId = item.ReceiverCompanyID,
                ReceiverPeppolId = item.ReceiverPeppolID,
                Sender = sender,
                File = file != null
                    ? new PeppolFile
                    {
                        FileID = file.FileID,
                        FileName = file.FileName,
                        ContentType = file.MimeType,
                        HasDocuments = file.HasDocuments
                    }
                    : null
            };
        }
    }

    async Task<PeppolSender?> GetParticipant(string senderPeppolId)
    {
        var identifier = senderPeppolId.Split(':').LastOrDefault();
        var parties = await partyManager.List(new PartySearchObject { Q = identifier });
        return parties
            .Select(p => new PeppolSender
            {
                PartyID = p.PartyID,
                Name = p.Name,
                VATNumber = p.VATNumber
            })
            .FirstOrDefault();
    }
}