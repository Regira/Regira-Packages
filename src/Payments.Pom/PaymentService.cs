using System.Net;
using Regira.Invoicing.Payments.Abstractions;
using Regira.Invoicing.Payments.Enums;
using Regira.Invoicing.Payments.Models;
using Regira.Serializing.Abstractions;
using System.Net.Http.Json;

namespace Regira.Payments.Pom;

public class PaymentService(PomSettings settings, ISerializer serializer) : IPaymentService
{
    private string? _authToken;


    public async Task<IPayment?> Details(string paymentId)
    {
        using var client = await GetPomClient();
        var url = settings.PaylinkStatusApi
            .Replace("{paymentId}", WebUtility.UrlEncode(paymentId));
        var response = await client.GetAsync(url);
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
    public async Task Save(IPayment payment)
    {
        var item = Convert(payment);
        using var client = await GetPomClient();
        var url = settings.CreatePaylinkApi;
        var response = await client.PostAsJsonAsync(url, item);
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
        item.Id = paymentResponse.PomDocumentId;
    }

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