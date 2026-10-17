namespace Regira.Invoicing.Billit.Models.Webhooks;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public class WebhookInput
{
    public string EntityType { get; set; } = "Order";
    public string EntityUpdateType { get; set; } = "I";
    public string WebhookURL { get; set; } = null!;
}