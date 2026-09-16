namespace Regira.Office.Word.Aspose;

/// <summary>
/// Configuration for <see cref="WordService"/>.
/// </summary>
/// <remarks>
/// Without a licence Aspose.Words runs in evaluation mode: it adds an evaluation watermark to every
/// document it loads or saves and truncates documents beyond a few hundred paragraphs.
/// When neither property is set, the <c>ASPOSE_WORDS_LICENSE</c> (Base64 content) and
/// <c>ASPOSE_WORDS_LICENSE_PATH</c> environment variables are used, in that order. When none of the four
/// is set either, constructing <see cref="WordService"/> throws, unless <see cref="AllowEvaluation"/> is on
/// or the process already holds a licence.
/// </remarks>
public class AsposeWordConfig
{
    /// <summary>
    /// Path to the Aspose licence file (<c>.lic</c>).
    /// </summary>
    public string? LicensePath { get; set; }

    /// <summary>
    /// The licence file's content, Base64-encoded — for hosts that keep it in a secret store rather
    /// than on disk. Takes precedence over <see cref="LicensePath"/>.
    /// </summary>
    public string? LicenseBase64 { get; set; }

    /// <summary>
    /// Accepts evaluation output when no licence is configured: watermarked documents, and long ones cut
    /// short. Off by default, so a licence setting that resolves to nothing fails at startup rather than in
    /// the documents a user receives.
    /// </summary>
    public bool AllowEvaluation { get; set; }
}
