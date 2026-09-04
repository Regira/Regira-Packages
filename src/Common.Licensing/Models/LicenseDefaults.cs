namespace Regira.Licensing.Models;

/// <summary>
/// Centralised product codes and free-tier default limit sets for Regira licenses.
/// Free-tier limits are compile-time constants used as a runtime fallback for unlicensed consumers.
/// Paid-tier limits are baked into the signed key by the license generator and configured there.
/// </summary>
public static class LicenseDefaults
{
    public static class Products
    {
        public const string Entities = "regira.entities";
        public const string Services = "regira.services";
        public const string Mcp = "regira.mcp";
    }

    /// <summary>
    /// Whether <paramref name="tier"/> is one that pays: anything named and not <c>free</c>. The one place
    /// that question is answered, so a host deciding limits from a <see cref="LicenseStatus"/> (which carries
    /// the tier as a bare string) and <see cref="License.IsPaid"/> cannot come to different conclusions about
    /// one key — including once a tier that is neither <c>free</c> nor paid, a trial say, is introduced here.
    /// </summary>
    public static bool IsPaidTier(string? tier) =>
        !string.IsNullOrEmpty(tier) &&
        !string.Equals(tier, "free", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compile-time free-tier limit values. Free-tier limits are a runtime fallback for unlicensed
    /// consumers, so they live in code (not configuration); paid-tier limits are baked into the signed
    /// key by the license generator and configured there instead.
    /// </summary>
    public static class Free
    {
        public static class Entities { public const int Simple = 5, Complex = 2; }
        public static class Services { public const int Permit = 5, Window = 60, Queue = 0; }
        // 30/60s: the canonical agent bootstrap flow (bootstrap -> recommend -> toc -> section-toc ->
        // several get_package calls) runs ~8-12 requests in one scaffolding turn; 30 keeps that from
        // 429-ing mid-task while staying abuse-resistant.
        public static class Mcp { public const int Permit = 30, Window = 60, Queue = 0; }
    }

    /// <summary>Free-tier limits applied to <see cref="Products.Entities"/> licenses.</summary>
    public static readonly IReadOnlyDictionary<string, int> EntityFreeLimits = new Dictionary<string, int>
    {
        ["entities.simple"]  = Free.Entities.Simple,
        ["entities.complex"] = Free.Entities.Complex
    };

    /// <summary>Rate-limit entries applied to free-tier <see cref="Products.Services"/> licenses.</summary>
    public static readonly IReadOnlyDictionary<string, int> ServiceFreeLimits = new Dictionary<string, int>
    {
        ["services.ratelimit.permit"] = Free.Services.Permit,
        ["services.ratelimit.window"] = Free.Services.Window,
        ["services.ratelimit.queue"]  = Free.Services.Queue
    };

    /// <summary>Rate-limit entries applied to free-tier <see cref="Products.Mcp"/> licenses.</summary>
    public static readonly IReadOnlyDictionary<string, int> McpFreeLimits = new Dictionary<string, int>
    {
        ["mcp.ratelimit.permit"] = Free.Mcp.Permit,
        ["mcp.ratelimit.window"] = Free.Mcp.Window,
        ["mcp.ratelimit.queue"]  = Free.Mcp.Queue
    };
}
