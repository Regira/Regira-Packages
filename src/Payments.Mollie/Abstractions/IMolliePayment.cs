using Regira.Payments.Mollie.Models;

namespace Regira.Payments.Mollie.Abstractions
{
    public interface IMolliePayment
    {
        string? Id { get; set; }
        MollieAmount Amount { get; set; }
        string? Description { get; set; }
        MolliePaymentStatus? Status { get; set; }
        IDictionary<string, object?>? Metadata { get; set; }
        DateTime? CreatedAt { get; set; }
    }
}
