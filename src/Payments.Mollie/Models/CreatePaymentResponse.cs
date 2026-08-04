namespace Regira.Payments.Mollie.Models;

public record CreatePaymentResponse
{
    public string Id { get; set; } = null!;
    public string? CheckoutUrl { get; set; }
}