using Regira.Licensing.Models;
using Regira.Office.Clients.Abstractions;

namespace Regira.Office.Clients.Services;

public class LicenseStatusClient(HttpClient client) : OfficeClientBase(client), ILicenseStatusClient
{
    private const string StatusPath = "license/status";

    public async Task<LicenseStatus> GetStatus(CancellationToken cancellationToken = default)
        => await GetJsonAsync<LicenseStatus>(StatusPath, cancellationToken)
           ?? throw new HttpRequestException($"{StatusPath} returned an empty response.");
}
