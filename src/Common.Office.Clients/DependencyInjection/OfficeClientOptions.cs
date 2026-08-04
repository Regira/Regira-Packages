namespace Regira.Office.Clients.DependencyInjection;

public class OfficeClientOptions
{
    public string BaseUrl { get; set; } = null!;
    [Obsolete("Use License instead: services.UseRegira(LICENSE_KEY)", false)]
    public string? ApiKey { get; set; }
}
