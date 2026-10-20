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

namespace Regira.Payments.Mollie.Services;

// https://github.com/Viincenttt/MollieApi
public class PaymentService(MollieConfig config)
{
    // every instance shares one connection pool, recycled so a change to Mollie's DNS reaches a long-running process;
    // no cookies, so nothing one instance receives rides on another's requests
    private static readonly SocketsHttpHandler SharedHandler = new() { PooledConnectionLifetime = TimeSpan.FromMinutes(2), UseCookies = false };

    private readonly int _maxPageSize = config.MaxPageSize;
    // the check Mollie's client makes, at construction as before
    private readonly string _key = string.IsNullOrWhiteSpace(config.Key) ? throw new ArgumentNullException("apiKey", "Mollie API key cannot be empty") : config.Key;
    private readonly Func<IPayment, string>? _webhookFactory = config.WebhookFactory;
    private readonly Func<IPayment, string>? _redirectFactory = config.RedirectFactory;


    // the handler every client goes through; a test points it at a stub
    internal HttpMessageHandler Handler { get; init; } = SharedHandler;

    private PaymentClient CreatePaymentClient() => new(_key, new HttpClient(Handler, disposeHandler: false));


    public async Task<IPayment?> Details(object id)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        using var paymentClient = CreatePaymentClient();
        var response = (await paymentClient.GetPaymentAsync(id.ToString()!)).EnsureSuccess();

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
        using var paymentClient = CreatePaymentClient();
        so.TryGetValue("PageSize", out int pageSize);
        IPayment? lastItem = null;
        int count;
        do
        {
            var limit = Math.Min(pageSize, _maxPageSize);
            var response = (await paymentClient.GetPaymentListAsync(lastItem?.Id, limit > 0 ? limit : null)).EnsureSuccess();

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
        using var paymentClient = CreatePaymentClient();
        return await Save(paymentClient, item);
    }
    /// <summary>
    /// Creates the payment as <see cref="Save(IPayment)"/> does, under <paramref name="idempotencyKey"/>:
    /// a repeat with the same key, after a timeout say, gets the first payment back rather than creating a second one.
    /// </summary>
    /// <param name="item">The payment to create; its <see cref="IPayment.Id"/> is set.</param>
    /// <param name="idempotencyKey">A key unique to this payment, such as the id of the order it pays for.</param>
    public async Task<CreatePaymentResponse> Save(IPayment item, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        }

        using var paymentClient = CreatePaymentClient();
        using (paymentClient.WithIdempotencyKey(idempotencyKey))
        {
            return await Save(paymentClient, item);
        }
    }
    private async Task<CreatePaymentResponse> Save(PaymentClient paymentClient, IPayment item)
    {
        var request = Convert(item);
        var response = (await paymentClient.CreatePaymentAsync(request)).EnsureSuccess();
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
        using var paymentClient = CreatePaymentClient();
        (await paymentClient.CancelPaymentAsync(item.Id)).EnsureSuccess();
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
            Amount = response.Amount.Value,
            Currency = response.Amount.Currency,
            Status = Convert(response.Status),
            Description = response.Description,
            CreatedAt = response.CreatedAt.LocalDateTime,
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