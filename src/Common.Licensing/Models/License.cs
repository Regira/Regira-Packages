using Regira.Licensing.Services;
using System.Text.Json.Serialization;

namespace Regira.Licensing.Models;

public class License
{
    [JsonPropertyName("cid")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("products")]
    public ICollection<string> Products { get; set; } = new List<string>();

    [JsonPropertyName("v")]
    public string? Version { get; set; }

    [JsonPropertyName("iat")]
    public long IssuedAtUnix { get; set; }

    [JsonPropertyName("exp")]
    public long? ExpiresAtUnix { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("devs")]
    public int Developers { get; set; } = 1;

    [JsonPropertyName("lim")]
    public Dictionary<string, int>? Limits { get; set; }

    [JsonIgnore]
    public DateTimeOffset IssuedAt => DateTimeOffset.FromUnixTimeSeconds(IssuedAtUnix);

    [JsonIgnore]
    public DateTimeOffset? ExpiresAt => ExpiresAtUnix.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(ExpiresAtUnix.Value)
        : null;

    [JsonIgnore]
    public int? MajorVersion => Version != null
        && Version.Split('.') is { Length: > 0 } parts
        && int.TryParse(parts[0], out var major)
        ? major
        : null;

    /// <summary>
    /// The original signed license key string. Set by <see cref="LicenseParser.Parse"/> and used
    /// by <c>LicenseValidator.Validate(License, string)</c> to re-verify the RSA signature.
    /// </summary>
    [JsonIgnore]
    public string? RawKey { get; internal set; }

    /// <summary>
    /// Returns <c>true</c> when the license is not a free tier (i.e. it has a non-null, non-"free" Tier value).
    /// </summary>
    [JsonIgnore]
    public bool IsPaid =>
        !string.IsNullOrEmpty(Tier) &&
        !string.Equals(Tier, "free", StringComparison.OrdinalIgnoreCase);
}
