namespace Regira.Office.Word.Syncfusion;

/// <summary>
/// Configuration for <see cref="WordService"/>.
/// </summary>
public class SyncfusionWordConfig
{
    /// <summary>
    /// Syncfusion license key. Without one, DocIO stamps trial text into every document it produces.
    /// <para>
    /// When left empty, the <c>SYNCFUSION_LICENSE_KEY</c> environment variable is used.
    /// </para>
    /// </summary>
    public string? LicenseKey { get; set; }
}
