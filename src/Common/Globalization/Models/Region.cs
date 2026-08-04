namespace Regira.Globalization.Models;

/// <summary>
/// Represents a country (or geographic region) with its associated ISO code, currency, and localized names.
/// </summary>
public record Country
{
    /// <summary>
    /// 2-letter ISO 3166 region code (e.g., "US", "BE")
    /// </summary>
    public string Iso2Code { get; set; } = null!;

    /// <summary>
    /// 3-letter ISO 3166 region code (e.g., "USA", "BEL")
    /// </summary>
    public string? Iso3Code { get; set; }

    /// <summary>
    /// English name of the region
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Native name of the region in its primary language
    /// </summary>
    public string? NativeName { get; set; }

    /// <summary>
    /// 3-letter ISO 4217 currency code (e.g., "USD", "EUR")
    /// </summary>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Currency symbol (e.g., "$", "€")
    /// </summary>
    public string? CurrencySymbol { get; set; }

    /// <summary>
    /// Localized display names per language ISO-2 code (e.g., "en", "fr")
    /// </summary>
    public IDictionary<string, string> Names { get; set; } = null!;
}