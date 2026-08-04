namespace Regira.Invoicing.Billit.Models.Webhooks;

public class WebhookInput
{
    public string EntityType { get; set; } = "Order";
    public string EntityUpdateType { get; set; } = "I";
    public string WebhookURL { get; set; } = null!;
}