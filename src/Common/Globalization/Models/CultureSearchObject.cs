using System.Globalization;

namespace Regira.Globalization.Models;

public record CultureSearchObject
{
    public string? Q { get; set; }
    public string? LanguageQ { get; set; }
    public string? CountryQ { get; set; }

    /// <summary>
    /// The ISO 639-1 two-letter language code or ISO 639-2 three-letter language code to filter cultures by language. If null, no filtering is applied.
    /// </summary>
    public ICollection<string>? LanguageCodes { get; set; }
    /// <summary>
    /// The ISO 3166-1 alpha-2 country code to filter cultures by country/region. If null, no filtering is applied.
    /// </summary>
    public ICollection<string>? CountryCodes { get; set; }
    /// <summary>
    /// The types of cultures to include in the search. If null, it defaults to CultureTypes.AllCultures, meaning all culture types will be included.
    /// </summary>
    public CultureTypes? CultureTypes { get; set; }
    /// <summary>
    /// Indicates whether to filter for custom cultures (LCID 4096) or not. If null, no filtering is applied.
    /// Default is false, meaning by default it will return non-custom cultures.
    /// </summary>
    public bool? IsCustomCulture { get; set; } = false;
    public bool? IsInvariantCulture { get; set; } = false;
}