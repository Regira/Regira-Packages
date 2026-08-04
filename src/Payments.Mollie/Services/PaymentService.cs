using Mollie.Api.Client;
using Mollie.Api.Models;
using Mollie.Api.Models.Payment.Request;
using Mollie.Api.Models.Payment.Response;
using Regira.Invoicing.Payments.Abstractions;
using Regira.Invoicing.Payments.Enums;
using Regira.Invoicing.Payments.Models;
using Regira.Payments.Mollie.Config;
using Regira.Payments.Mollie.Models;
using Regira.Utilities;
using System.Globalization;

namespace Regira.Payments.Mollie.Services;

// https://github.com/Viincenttt/MollieApi
public class PaymentService(MollieConfig config)
{
    private readonly int _maxPageSize = config.MaxPageSize;
    private readonly PaymentClient _paymentClient = new(config.Key);
    private readonly Func<IPayment, string>? _webhookFactory = config.WebhookFactory;
    private readonly Func<IPayment, string>? _redirectFactory = config.RedirectFactory;


    public async Task<IPayment?> Details(object id)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        var response = await _paymentClient.GetPaymentAsync(id.ToString()!);

        if (response == null!)
        {
            return null;
        }

        return Convert(response);
    }
    public async Task<IEnumerable<IPayment>> List(object? o = null)
    {
        var list = new List<IPayment>();
        var so = DictionaryUtility.ToDictionary(o);
        so.TryGetValue("PageSize", out int pageSize);
        IPayment? lastItem = null;
        int count;
        do
        {
            var limit = Math.Min(pageSize, _maxPageSize);
            var response = await _paymentClient.GetPaymentListAsync(lastItem?.Id, limit > 0 ? limit : null);

            IEnumerable<IPayment> items = response.Items
                .Select(Convert)
                .ToList();
            count = response.Count;
            lastItem = items.LastOrDefault();
            items = items
                .Skip(list.Count == 0 ? 0 : 1)
                .FilterItems(so);

            list.AddRange(items);

        } while (count >= _maxPageSize && (pageSize == 0 || list.Count < pageSize));

        if (pageSize > 0)
        {
            return list
                .Take(pageSize)
                .ToList();
        }

        return list;
    }

    public async Task<CreatePaymentResponse> Save(IPayment item)
    {
        var request = Convert(item);
        var response = await _paymentClient.CreatePaymentAsync(request);
        item.Id = response.Id;
        var checkoutUrl = response.Links.Checkout?.Href;
        return new CreatePaymentResponse
        {
            Id = response.Id,
            CheckoutUrl = checkoutUrl ?? string.Empty
        };
    }
    public async Task Delete(IPayment item)
    {
        // Not supported by Mollie anymore
        //await _paymentClient.DeletePaymentAsync(item.Id);
        await _paymentClient.CancelPaymentAsync(item.Id);
    }

    public async Task WebHook(string id, Func<IPayment?, Task> handleWebHook)
    {
        var payment = await Details(id);
        await handleWebHook.Invoke(payment);
    }

    IPayment Convert(PaymentResponse response)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        return new Payment
        {
            Id = response.Id,
            Amount = decimal.Parse(response.Amount.Value, NumberStyles.Number, CultureInfo.InvariantCulture),
            Currency = response.Amount.Currency,
            Status = Convert(response.Status),
            Description = response.Description,
            CreatedAt = response.CreatedAt,
            Metadata = !string.IsNullOrEmpty(response.Metadata)
                ? response.GetMetadata<IDictionary<string, object?>>()
                : new Dictionary<string, object?>()
        };
    }

    PaymentStatus? Convert(string? status)
    {
        return string.IsNullOrEmpty(status)
            ? PaymentStatus.Open
            : Enum.Parse<PaymentStatus>(status, true);
    }
    PaymentRequest Convert(IPayment payment)
    {
        if (payment == null)
        {
            throw new ArgumentNullException(nameof(payment));
        }

        var request = new PaymentRequest
        {
            Amount = new Amount(payment.Currency ?? "EUR", payment.Amount),
            Description = payment.Description ?? string.Empty,
            RedirectUrl = _redirectFactory?.Invoke(payment),
            WebhookUrl = _webhookFactory?.Invoke(payment)
        };
        var metadata = payment.Metadata;
        if (metadata?.Any() == true)
        {
            request.SetMetadata(metadata);
        }
        return request;
    }
}