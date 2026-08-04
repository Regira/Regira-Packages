namespace Regira.Utilities;

/// <summary>
/// Ambient (process-wide) policy for how <see cref="DateTime"/> values are handled. One policy per process.
/// </summary>
public static class DateTimeDefaults
{
    /// <summary>
    /// When <c>true</c> (default), <see cref="DateTime"/> values are handled as UTC:
    /// timestamp primers write <see cref="DateTime.UtcNow"/> and normalize client-supplied values,
    /// query filters normalize input dates, and <c>UtcDateTimeConverter</c> rounds values through
    /// the database as UTC (local kinds are converted, unspecified kinds are assumed UTC).<br />
    /// When <c>false</c>, timestamp primers write <see cref="DateTime.Now"/>, the converter passes
    /// values through unchanged and <see cref="DateTime"/> values are used exactly as given.<br />
    /// Configure via <c>UseEntities(e =&gt; e.UseUtc(...))</c> or set directly at startup,
    /// before the EF model is built.
    /// </summary>
    public static bool UseUtc { get; set; } = true;
}
