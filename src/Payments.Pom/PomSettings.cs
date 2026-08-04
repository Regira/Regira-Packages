namespace Regira.Payments.Pom;

public class PomSettings
{
    public string? SenderId { get; set; }
    public string? SenderContractNumber { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? AppName { get; set; }
    public int ExpiresIn { get; set; }
    public string? Platform { get; set; }
    public string? EnvironmentId { get; set; }
    public string? WebhookKey { get; set; }
    public string? Mode { get; set; }

    public string? AuthApi { get; set; }
    public string PaylinkStatusApi { get; set; } = null!;
    public string CreatePaylinkApi { get; set; } = null!;
}