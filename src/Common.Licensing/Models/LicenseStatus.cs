using Regira.Licensing.Services;

namespace Regira.Licensing.Models;

/// <summary>
/// What a licensed product made of a presented license key. Produced by
/// <see cref="LicenseValidator.GetStatus"/>; served by the hosted Regira APIs and the MCP server so a
/// consumer can ask why its limits changed, and when a key needs renewing, without a key that still works.
/// For the four states that are not accepted, what happens next is the product's decision: the hosted
/// services and the MCP server fall back to the free tier, the in-process modules refuse to start.
/// </summary>
public enum LicenseState
{
    /// <summary>No key was presented.</summary>
    Missing,
    /// <summary>The key could not be read or its signature does not verify; it is ignored.</summary>
    Invalid,
    /// <summary>The key is genuine but does not cover the product.</summary>
    NotAccepted,
    /// <summary>The key expired and is no longer accepted.</summary>
    Expired,
    /// <summary>The key is genuine and covers the product, but for another major version than the one in use.</summary>
    VersionMismatch,
    /// <summary>The key expired but is still accepted for a short grace period; renew now.</summary>
    ExpiredInGrace,
    /// <summary>The key is valid but expires within <see cref="LicenseValidator.RenewalReminderPeriod"/>; renew now.</summary>
    ExpiringSoon,
    /// <summary>The key is valid.</summary>
    Valid
}

/// <summary>
/// Status of a presented license key for one product. Plain properties so it round-trips as JSON between
/// the hosted APIs and their clients.
/// </summary>
public class LicenseStatus
{
    public LicenseState State { get; set; }
    /// <summary>The product the key was checked against, e.g. <c>regira.services</c>.</summary>
    public string ProductCode { get; set; } = null!;
    /// <summary>True when the product treats the key as licensed, so the key's own limits apply instead of the free tier.</summary>
    public bool Accepted { get; set; }
    public string? CustomerId { get; set; }
    /// <summary>The key's tier (<c>paid</c>, <c>trial</c>, ...); null when the key could not be read.</summary>
    public string? Tier { get; set; }
    public string[] Products { get; set; } = [];
    public DateTimeOffset? IssuedAt { get; set; }
    /// <summary>Null for a perpetual key and for a key that could not be read.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>Whole days until <see cref="ExpiresAt"/>, negative once it has passed; null when there is no expiry date.</summary>
    public int? DaysUntilExpiry { get; set; }
    /// <summary>
    /// The limits carried by the key itself. Null for a commercial key without baked limits (unlimited) and for a
    /// key that could not be read. Which limits actually apply is decided by the product: only when
    /// <see cref="Accepted"/> is true.
    /// </summary>
    public Dictionary<string, int>? Limits { get; set; }
    /// <summary>One human-readable sentence describing the state, including the renewal link where relevant.</summary>
    public string Message { get; set; } = null!;
    /// <summary>
    /// What the answering product does with the key — the limits it applies, or that it refuses the key.
    /// Never set by <see cref="LicenseValidator.GetStatus"/>, which describes only the key; each host fills it in.
    /// Null when the status was produced without a host.
    /// </summary>
    public string? Applied { get; set; }
}
