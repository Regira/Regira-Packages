using Regira.Invoicing.Payments.Abstractions;

namespace Regira.Payments.Mollie.Config;

public class MollieConfig
{
    public string Api { get; set; } = null!;
    public string Key { get; set; } = null!;
    public int MaxPageSize { get; set; } = 250;

    public Func<IPayment, string>? RedirectFactory { get; set; }
    public Func<IPayment, string>? WebhookFactory { get; set; }
}