using Regira.Invoicing.Billit.Config;
using Regira.Invoicing.Billit.Models.Contacts;
using System.Net.Http.Json;

namespace Regira.Invoicing.Billit.Services;

public interface IPartyManager
{
    Task<IList<PartyDto>> List(PartySearchObject? so = null);
    Task<PartyDto?> Get(string id);
}

public class PartyManager(IHttpClientFactory clientFactory) : IPartyManager
{
    private const string Path = "/v1/parties";
    private readonly HttpClient _client = clientFactory.CreateClient(BillitConstants.HttpClientName);


    public async Task<IList<PartyDto>> List(PartySearchObject? so = null)
    {
        so ??= new PartySearchObject();
        var url = $"{Path}?fullTextSearch={so.Q}";
        var response = await _client.GetFromJsonAsync<PartyListResponse>(url);
        return response!.Items;
    }
    public async Task<PartyDto?> Get(string id)
    {
        var url = $"{Path}/{id}";
        return await _client.GetFromJsonAsync<PartyDto>(url);
    }
}

public class PartySearchObject
{
    public string? Q { get; set; }
}