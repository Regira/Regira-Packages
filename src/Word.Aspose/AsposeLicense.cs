namespace Regira.Office.Word.Aspose;

/// <summary>
/// Applies the Aspose.Words licence once per process.
/// </summary>
/// <remarks>
/// Aspose asks for the licence to be set once per application domain, before any other Aspose.Words
/// class is used. <see cref="WordService"/> therefore calls this from its constructor and holds no
/// static field of an Aspose type. A licence from a different setting or file handed over later is applied
/// as well; the same one is recognised by where it comes from, so the file is not read again.
/// </remarks>
internal static class AsposeLicense
{
    public const string LicenseVariable = "ASPOSE_WORDS_LICENSE";
    public const string LicensePathVariable = "ASPOSE_WORDS_LICENSE_PATH";

    private const string ConfigBase64 = $"{nameof(AsposeWordConfig)}.{nameof(AsposeWordConfig.LicenseBase64)}";
    private const string ConfigPath = $"{nameof(AsposeWordConfig)}.{nameof(AsposeWordConfig.LicensePath)}";

    /// <summary>
    /// Where a licence comes from: its Base64 content or its file path, and the setting that named it.
    /// </summary>
    private sealed record Location(string Setting, string? Base64, string? Path)
    {
        /// <summary>What a rejected licence is reported against: the file, or the setting.</summary>
        public string Source => Path ?? Setting;
        public string Key => Base64 != null ? $"base64:{Base64.Trim()}" : $"path:{System.IO.Path.GetFullPath(Path!)}";
    }

    // object rather than System.Threading.Lock: this package also targets net8.0.
    private static readonly object Gate = new();
    private static string? _registered;

    public static void Register(AsposeWordConfig? config)
        => Register(config, Environment.GetEnvironmentVariable);

    /// <param name="config">The configured licence, if any.</param>
    /// <param name="environment">Reads an environment variable; replaceable so the fallback can be tested
    /// without touching the process environment.</param>
    /// <exception cref="InvalidOperationException">No licence is configured and <see cref="AsposeWordConfig.AllowEvaluation"/>
    /// is off, or the configured licence cannot be read or is rejected</exception>
    internal static void Register(AsposeWordConfig? config, Func<string, string?> environment)
    {
        var location = Locate(config, environment);
        if (location == null)
        {
            // a licence applied earlier covers the whole process
            if (config?.AllowEvaluation == true || _registered != null)
            {
                return;
            }
            throw new InvalidOperationException(
                $"No Aspose.Words licence is configured: set {ConfigPath} or {ConfigBase64}, or the {LicensePathVariable} or {LicenseVariable} environment variable. " +
                $"Unlicensed, Aspose.Words watermarks every document and cuts long ones short; set {nameof(AsposeWordConfig)}.{nameof(AsposeWordConfig.AllowEvaluation)} to accept that output.");
        }

        var key = location.Key;
        if (_registered == key)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered == key)
            {
                return;
            }

            var bytes = Read(location);
            try
            {
                using var stream = new MemoryStream(bytes);
                new global::Aspose.Words.License().SetLicense(stream);
            }
            catch (Exception ex)
            {
                // a rejected licence can leave Aspose in evaluation mode, so an earlier one no longer counts as applied
                _registered = null;
                throw new InvalidOperationException($"Aspose.Words rejected the licence from {location.Source}: {ex.Message}", ex);
            }
            _registered = key;
        }
    }

    /// <summary>
    /// The licence file's bytes and where they came from, or <c>(null, null)</c> when none is configured.
    /// </summary>
    internal static (byte[]? Bytes, string? Source) Resolve(AsposeWordConfig? config)
        => Resolve(config, Environment.GetEnvironmentVariable);

    /// <inheritdoc cref="Resolve(AsposeWordConfig?)"/>
    /// <param name="config">The configured licence, if any.</param>
    /// <param name="environment">Reads an environment variable.</param>
    internal static (byte[]? Bytes, string? Source) Resolve(AsposeWordConfig? config, Func<string, string?> environment)
    {
        var location = Locate(config, environment);
        return location == null ? (null, null) : (Read(location), location.Source);
    }

    /// <summary>
    /// Config Base64, config path, environment Base64, environment path — the first one set. Blank counts as unset.
    /// </summary>
    private static Location? Locate(AsposeWordConfig? config, Func<string, string?> environment)
    {
        if (!string.IsNullOrWhiteSpace(config?.LicenseBase64))
        {
            return new Location(ConfigBase64, config.LicenseBase64, null);
        }
        if (!string.IsNullOrWhiteSpace(config?.LicensePath))
        {
            return new Location(ConfigPath, null, config.LicensePath);
        }

        var content = environment(LicenseVariable);
        if (!string.IsNullOrWhiteSpace(content))
        {
            return new Location(LicenseVariable, content, null);
        }
        var path = environment(LicensePathVariable);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return new Location(LicensePathVariable, null, path);
        }

        return null;
    }

    private static byte[] Read(Location location)
    {
        if (location.Base64 != null)
        {
            try
            {
                return Convert.FromBase64String(location.Base64.Trim());
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"{location.Setting} must hold the Aspose licence file's content, Base64-encoded.", ex);
            }
        }

        try
        {
            return File.ReadAllBytes(location.Path!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"The Aspose licence file {location.Path}, named by {location.Setting}, cannot be read: {ex.Message}", ex);
        }
    }
}
