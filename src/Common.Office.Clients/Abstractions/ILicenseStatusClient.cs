using Regira.Licensing.Models;

namespace Regira.Office.Clients.Abstractions;

/// <summary>
/// Asks the hosted Regira Office API what it makes of this application's <c>regira.services</c> license key —
/// the key <c>AddOfficeClients</c> sends with every call. The endpoint answers for any key, including an
/// expired one, so this is the way to learn why calls are suddenly rate-limited or when to renew.
/// </summary>
public interface ILicenseStatusClient
{
    Task<LicenseStatus> GetStatus(CancellationToken cancellationToken = default);
}
