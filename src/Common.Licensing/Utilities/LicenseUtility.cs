using Regira.Licensing.Models;

namespace Regira.Licensing.Utilities;

/// <summary>
/// Helper operations over <see cref="License"/> instances and the free-tier defaults in
/// <see cref="LicenseDefaults"/>.
/// </summary>
public static class LicenseUtility
{
    /// <summary>
    /// Returns a free-tier <see cref="License"/> with all known products and their default
    /// free-tier limits pre-applied.
    /// </summary>
    public static License CreateFree() =>
        ApplyFreeLimits(new License
        {
            Tier     = "free",
            Products = [LicenseDefaults.Products.Entities, LicenseDefaults.Products.Services, LicenseDefaults.Products.Mcp]
        });

    /// <summary>
    /// Merges the free-tier default limits for each of the license's declared products into
    /// <see cref="License.Limits"/> (existing entries are overwritten), and returns the license.
    /// </summary>
    public static License ApplyFreeLimits(License license)
    {
        var products = license.Products.Select(p => p.ToLowerInvariant()).ToHashSet();
        var limits = license.Limits is null
            ? new Dictionary<string, int>()
            : new Dictionary<string, int>(license.Limits);

        if (products.Contains(LicenseDefaults.Products.Entities))
            foreach (var (k, v) in LicenseDefaults.EntityFreeLimits) limits[k] = v;
        if (products.Contains(LicenseDefaults.Products.Services))
            foreach (var (k, v) in LicenseDefaults.ServiceFreeLimits) limits[k] = v;
        if (products.Contains(LicenseDefaults.Products.Mcp))
            foreach (var (k, v) in LicenseDefaults.McpFreeLimits) limits[k] = v;

        license.Limits = limits;
        return license;
    }

    /// <summary>
    /// Returns the best license for <paramref name="productCode"/> from <paramref name="licenses"/>,
    /// preferring paid tiers over free. Returns <c>null</c> when none match.
    /// </summary>
    public static License? Resolve(IEnumerable<License> licenses, string productCode) =>
        licenses
            .Where(l => l.Products.Any(p => string.Equals(p, productCode, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(l => l.IsPaid ? 1 : 0)
            .FirstOrDefault();


    /// <summary>
    /// Populates <see cref="License.Limits"/> from the free-tier constants (free tier) or the configured
    /// paid-tier limits (any other tier). Commercial tiers without a configured entry receive no limits.
    /// <paramref name="limitsConfig"/> is keyed product -&gt; tier -&gt; limitKey -&gt; value.
    /// </summary>
    public static License ApplyLimits(License license, IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, int>>> limitsConfig)
    {
        var isFree = string.Equals(license.Tier, "free", StringComparison.OrdinalIgnoreCase);

        if (isFree)
        {
            ApplyFreeLimits(license);
            license.Limits = license.Limits is { Count: > 0 } ? license.Limits : null;
            return license;
        }

        var limits = new Dictionary<string, int>();
        if (license.Tier is { } tier)
        {
            var productSet = license.Products.Select(p => p.ToLowerInvariant()).ToHashSet();
            var tierKey = tier.ToLowerInvariant();
            foreach (var product in productSet)
                if (limitsConfig.TryGetValue(product, out var perTier)
                    && perTier.TryGetValue(tierKey, out var productLimits))
                    foreach (var (k, v) in productLimits) limits[k] = v;
        }

        license.Limits = limits.Count > 0 ? limits : null;
        return license;
    }
}
