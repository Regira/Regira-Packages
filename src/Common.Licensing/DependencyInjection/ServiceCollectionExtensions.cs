using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Licensing.Models;
using Regira.Licensing.Services;
using Regira.Licensing.Utilities;

namespace Regira.Licensing.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Reads license keys from <c>configuration["Regira:LicenseKeys"]</c> and registers each parsed
    /// <see cref="License"/> as a singleton, writing its settings to the console.
    /// </summary>
    public static IServiceCollection UseRegira(this IServiceCollection services, IConfiguration configuration)
    {
        var keys = new[] { configuration["Regira:LicenseKey"] } // legacy singular key, still honoured
            .Concat(configuration.GetSection("Regira:LicenseKeys")
            .GetChildren().Select(s => s.Value))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .OfType<string>()
            .ToArray();
        return services.UseRegira(keys);
    }

    /// <summary>
    /// Parses and registers each Regira license key as a singleton, writing its settings to the console.
    /// A key that expires within <see cref="LicenseValidator.RenewalReminderPeriod"/> additionally writes a renewal
    /// reminder to the console; a key past its expiry date writes a warning to the console's error stream.
    /// A null or empty key registers a free-tier <see cref="License"/> with the default limits from
    /// <see cref="LicenseDefaults"/>; calling with no keys registers nothing and the licensed modules
    /// fall back to the free tier automatically.
    /// A single key can cover multiple products; pass several keys to combine them — they coexist and
    /// the system picks the best (paid over free) license per product.
    /// </summary>
    public static IServiceCollection UseRegira(this IServiceCollection services, params string?[] licenseKeys)
    {
        foreach (var licenseKey in licenseKeys)
        {
            var license = string.IsNullOrWhiteSpace(licenseKey)
                ? LicenseUtility.CreateFree()
                : LicenseParser.Parse(licenseKey);

            var customerId = license.CustomerId ?? "(none)";
            var products = string.Join(", ", license.Products);
            var issuedAt = license.IssuedAt.ToString("yyyy-MM-dd");
            var expiresAt = license.ExpiresAt?.ToString("yyyy-MM-dd") ?? "never";
            var limits = license.Limits?.Count > 0
                ? string.Join(", ", license.Limits.Select(kv => $"{kv.Key}={kv.Value}"))
                : "(none)";
            Version.TryParse(license.Version ?? "0.0.0.0", out var version);

            Console.WriteLine($"[Regira] License — Customer: {customerId} | Products: {products} | Version: {version?.Major} | Issued: {issuedAt} | Expires: {expiresAt} | Limits: {limits}");

            var now = DateTimeOffset.UtcNow;
            if (LicenseValidator.IsRefusedAsExpired(license, now))
                Console.Error.WriteLine($"[Regira] ERROR: The license for {products} expired on {expiresAt} and is no longer accepted. Renew at https://regira.com/licensing");
            else if (LicenseValidator.IsPastExpiry(license, now))
                Console.Error.WriteLine($"[Regira] WARNING: The license for {products} expired on {expiresAt}. It will stop working any time soon — renew at https://regira.com/licensing");
            else if (LicenseValidator.IsExpiringSoon(license, now))
            {
                // Standard output on purpose: collectors file stderr as error level, and a healthy application
                // with days left on its key should not start with an error line.
                var daysLeft = Math.Max(0, (int)Math.Ceiling((license.ExpiresAt!.Value - now).TotalDays));
                Console.WriteLine($"[Regira] Reminder: the license for {products} expires in {daysLeft} day(s), on {expiresAt}. Renew now at https://regira.com/licensing");
            }

            services.AddSingleton(license);
        }

        return services;
    }
}
