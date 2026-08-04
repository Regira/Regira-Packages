namespace Regira.Invoicing.ViaAdValvas.Config;

public class GatewaySettings
{
    public string Uri { get; set; } = null!;
    public string SenderID { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public bool IsProduction { get; set; }
}