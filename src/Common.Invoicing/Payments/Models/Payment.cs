using Regira.Invoicing.Payments.Abstractions;
using Regira.Invoicing.Payments.Enums;

namespace Regira.Invoicing.Payments.Models;

public class Payment : IPayment
{
    public string? Id { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public PaymentStatus? Status { get; set; }
    public IDictionary<string, object?>? Metadata { get; set; }
    public DateTime? CreatedAt { get; set; }
}