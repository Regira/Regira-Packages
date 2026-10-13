using System.Net;
using Regira.Invoicing.Payments.Abstractions;
using Regira.Invoicing.Payments.Enums;
using Regira.Invoicing.Payments.Models;
using Regira.Serializing.Abstractions;
using System.Net.Http.Json;

namespace Regira.Payments.Pom;

/// <summary>
/// POM backend of <see cref="IPaymentService"/>.
/// </summary>
/// <param name="settings">Credentials and the POM endpoint URLs.</param>
/// <param name="serializer">A JSON serializer.</param>
public class PaymentService(PomSettings settings, ISerializer serializer) : IPaymentService
{
    // every instance shares one connection pool, recycled so a change to POM's DNS reaches a long-running process;
    // no cookies, so nothing one instance receives rides on another's requests
    private static readonly SocketsHttpHandler SharedHandler = new() { PooledConnectionLifetime = TimeSpan.FromMinutes(2), UseCookies = false };
    // the handler every client goes through; a test points it at a stub
    internal HttpMessageHandler Handler { get; init; } = SharedHandler;
    private string? _authToken;


    public async Task<IPayment?> Details(string paymentId)
    {
        var url = settings.PaylinkStatusApi
            .Replace("{paymentId}", WebUtility.UrlEncode(paymentId));
        var response = await Send(HttpMethod.Get, url);
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            response.EnsureSuccessStatusCode();
            var paymentResponse = serializer.Deserialize<PomPayment>(content);
            if (paymentResponse?.PaymentStatus == "NOT_FOUND")
            {
                return null;
            }

            return Convert(paymentResponse!);
        }
        catch (HttpRequestException ex)
        {
            throw GetPomException(ex, content, "Fetching POM payment failed");
        }
    }
    /// <summary>
    /// Creates a payment link for <paramref name="payment"/> and sets its <see cref="IPayment.Id"/>.
    /// <see cref="CreatePaylink"/> also returns the link.
    /// </summary>
    public Task Save(IPayment payment) => CreatePaylink(payment);
    /// <summary>
    /// Creates a payment link for <paramref name="payment"/> and sets its <see cref="IPayment.Id"/>.
    /// </summary>
    /// <returns>POM's response, with the link to send the payer to in <see cref="PomPaymentResponse.Paylink"/>.</returns>
    public async Task<PomPaymentResponse> CreatePaylink(IPayment payment)
    {
        var response = await Send(HttpMethod.Post, settings.CreatePaylinkApi, Convert(payment));
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw GetPomException(ex, content, "Saving POM payment failed");
        }
        var paymentResponse = serializer.Deserialize<PomPaymentResponse>(content)!;
        payment.Id = paymentResponse.PomDocumentId!;
        return paymentResponse;
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, PomPayment? body = null)
    {
        var response = await SendOnce(method, url, body);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // POM no longer accepts the token: authenticate again and send once more
        response.Dispose();
        _authToken = null;
        return await SendOnce(method, url, body);
    }
    private async Task<HttpResponseMessage> SendOnce(HttpMethod method, string url, PomPayment? body)
    {
        var httpClient = new HttpClient(Handler, disposeHandler: false);
        if (string.IsNullOrWhiteSpace(_authToken))
        {
            try
            {
                await Authenticate(httpClient);
            }
            catch (Exception ex)
            {
                throw new PomException("POM authentication failed", ex);
            }
        }

        using var request = new HttpRequestMessage(method, url);
        request.Content = body == null ? null : JsonContent.Create(body);
        request.Headers.Add("X-Authentication", _authToken);
        return await httpClient.SendAsync(request);
    }

    [Obsolete("PaymentService sends every request through a connection pool the package shares.")]
    protected async Task<HttpClient> GetPomClient()
    {
        var client = new HttpClient();
        if (string.IsNullOrWhiteSpace(_authToken))
        {
            // authenticate
            try
            {
                await Authenticate(client);
            }
            catch (Exception ex)
            {
                throw new PomException("POM authentication failed", ex);
            }
        }

        client.DefaultRequestHeaders.Add("X-Authentication", _authToken);
        return client;
    }

    protected PomException GetPomException(HttpRequestException ex, string pomResponse, string message = "POM error")
    {
        return new PomException($"{message} ({ex.StatusCode})", ex)
        {
            PomResponse = pomResponse,
            StatusCode = (int)ex.StatusCode!
        };
    }
    protected async Task Authenticate(HttpClient client)
    {
        var serializedSettings = serializer.Serialize(settings);
        var response = await client.PostAsync(settings.AuthApi, new StringContent(serializedSettings));
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw GetPomException(ex, content, "Authenticating failed");
        }
        var pomAuthResponse = serializer.Deserialize<PomAuthResponse>(content)!;
        _authToken = pomAuthResponse.AuthToken;
    }

    IPayment Convert(PomPayment item)
    {
        var payment = new Payment
        {
            Id = item.Id,
            Amount = item.Amount,
            Currency = item.Currency,
            CreatedAt = item.DocumentDate,
            Status = Convert(item.PaymentStatus)
        };
        return payment;
    }
    PaymentStatus? Convert(string? status)
    {
        switch (status)
        {
            case nameof(PaymentStatus.Open):
                return PaymentStatus.Open;
            case nameof(PaymentStatus.Canceled):
                return PaymentStatus.Canceled;
            case nameof(PaymentStatus.Pending):
                return PaymentStatus.Pending;
            case nameof(PaymentStatus.Authorized):
                return PaymentStatus.Authorized;
            case nameof(PaymentStatus.Expired):
                return PaymentStatus.Expired;
            case nameof(PaymentStatus.Failed):
                return PaymentStatus.Failed;
            case nameof(PaymentStatus.Paid):
                return PaymentStatus.Paid;
        }
        return null;
    }
    PomPayment Convert(IPayment payment)
    {
        var item = new PomPayment
        {
            SenderContractNumber = settings.SenderContractNumber,
            Amount = Math.Round(payment.Amount, 2)
        };
        if (!string.IsNullOrWhiteSpace(payment.Currency))
        {
            item.Currency = payment.Currency;
        }

        return item;
    }
}