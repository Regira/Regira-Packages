using Regira.Invoicing.Billit.Config;
using Regira.Invoicing.Billit.Mapping;
using Regira.Invoicing.Billit.Models.Orders.Input;
using Regira.Invoicing.Billit.Models.Results.Extensions;
using Regira.Invoicing.Invoices.Abstractions;
using Regira.Invoicing.Invoices.Models.Abstractions;
using Regira.Invoicing.Invoices.Models.Results;
using Regira.Serializing.Abstractions;
using System.Text;

namespace Regira.Invoicing.Billit.Services;

/// <summary>
/// Billit's <see cref="IInvoiceService"/> (<c>Create</c>, and <c>Send</c> of an invoice), plus sending invoices already
/// created by their ids.
/// </summary>
public interface IInvoiceManager : IInvoiceService
{
    Task<ISendInvoiceResult> Send(params string[] ids);
}

public class InvoiceManager(IHttpClientFactory clientFactory, ISerializer serializer) : IInvoiceManager
{
    private const string Path = "/v1/orders";
    private readonly HttpClient _client = clientFactory.CreateClient(BillitConstants.HttpClientName);

    public async Task<ICreateInvoiceResult> Create(IInvoice input)
    {
        var model = input.ToOrderInput();
        var json = serializer.Serialize(model);
        var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(Path, body);

        await response.ThrowWhenError();
        return new CreateInvoiceResult
        {
            InvoiceId = await response.Content.ReadAsStringAsync()
        };
    }

    public async Task<ISendInvoiceResult> Send(params string[] ids)
    {
        var url = $"{Path}/commands/send";
        var input = new SendOrderInput
        {
            OrderIDs = ids.Select(int.Parse).ToArray()
        };
        var json = serializer.Serialize(input);
        var body = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, body);
        await response.ThrowWhenError();

        return new SendInvoiceResult();
    }

    public async Task<ISendInvoiceResult> Send(IInvoice input)
    {
        var result = await Create(input);

        return await Send(result.InvoiceId);
    }
}