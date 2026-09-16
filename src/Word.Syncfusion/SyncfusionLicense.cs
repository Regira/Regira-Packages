using Syncfusion.Licensing;

namespace Regira.Office.Word.Syncfusion;

/// <summary>
/// Registers the Syncfusion license key once per process.
/// </summary>
/// <remarks>
/// <see cref="SyncfusionLicenseProvider.RegisterLicense"/> is process-global and appends every key it is
/// handed, so re-registering the same key on each request would grow an ever-longer string. Registration
/// must also happen before the first DocIO type is touched, which is why <see cref="WordService"/> calls
/// this from its constructor and holds no static field of a DocIO type.
/// </remarks>
internal static class SyncfusionLicense
{
    public const string EnvironmentVariable = "SYNCFUSION_LICENSE_KEY";

    // object rather than System.Threading.Lock: this package also targets net8.0.
    private static readonly object Gate = new();
    private static string? _registered;

    public static void Register(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable(EnvironmentVariable);
        }
        if (string.IsNullOrWhiteSpace(key) || _registered == key)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered == key)
            {
                return;
            }

            SyncfusionLicenseProvider.RegisterLicense(key);
            _registered = key;
        }
    }
}
