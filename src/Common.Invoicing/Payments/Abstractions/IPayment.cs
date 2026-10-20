using Regira.Invoicing.Payments.Enums;

namespace Regira.Invoicing.Payments.Abstractions;

public interface IPayment
{
    string Id { get; set; }
    decimal Amount { get; set; }
    string? Currency { get; set; }
    string? Description { get; set; }
    PaymentStatus? Status { get; set; }
    IDictionary<string, object?>? Metadata { get; set; }
    DateTime? CreatedAt { get; set; }
    /// <summary>
    /// When the payment link expires; <c>null</c> leaves it to the provider (POM: one day after the link is created).
    /// Read by the POM provider. The default member returns <c>null</c>, so an implementation without it still compiles;
    /// <see cref="Models.Payment"/> declares it settable.
    /// </summary>
    DateTime? ExpiresAt => null;
}