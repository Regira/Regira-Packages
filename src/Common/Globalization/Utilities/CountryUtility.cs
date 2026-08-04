using Regira.Globalization.Models;
using System.Globalization;

namespace Regira.Globalization.Utilities;

public static class CountryUtility
{
    public static IEnumerable<Country> GetCountries(CultureSearchObject? so = null)
        => [.. CultureUtility.GetAll((so ?? new CultureSearchObject()) with { CultureTypes = CultureTypes.SpecificCultures }).ToCountries()];

    /// <summary>
    /// Retrieves a region by its ISO 2-letter.
    /// </summary>
    /// <param name="code">The ISO 2-letter.</param>
    /// <returns>The region if found; otherwise, null.</returns>
    public static Country? GetCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return CultureUtility.GetAll(new CultureSearchObject { CultureTypes = CultureTypes.SpecificCultures, CountryCodes = [code] })
            .ToCountries()
            .FirstOrDefault();
    }


    // Extension methods
    public static IEnumerable<Country> GetCountries(this IEnumerable<CultureInfo>? cultures)
        => ToCountries(cultures ?? CultureUtility.GetAll(new CultureSearchObject() { CultureTypes = CultureTypes.SpecificCultures }));
    public static Country? GetCountry(this CultureInfo culture)
        => culture.TryGetRegionInfo()?.ToCountry();

    /// <summary>
    /// Gets the localized name of the country based on the provided ISO 639-1 language code. 
    /// If no language code is provided, it returns the default display name of the region.
    /// If a language code is provided but no localized name exists for that language, it falls back to the default title of the country.
    /// </summary>
    /// <param name="item">The country for which to get the name.</param>
    /// <param name="langIso2Code">The ISO 639-1 language code for localization.</param>
    /// <returns>The localized name of the country.</returns>
    public static string GetName(this Country item, string? langIso2Code = null)
    {
        if (string.IsNullOrWhiteSpace(langIso2Code))
        {
            var region = ToRegionInfo(item);
            return region.DisplayName;
        }

        return item.Names.TryGetValue(langIso2Code, out var localizedName)
            ? localizedName
            : item.Title;
    }
    public static ICollection<string> GetLanguages(this Country item)
        => item.Names.Keys;

    public static Country ToCountry(this RegionInfo regionInfo)
        => new()
        {
            Iso2Code = regionInfo.TwoLetterISORegionName,
            Iso3Code = regionInfo.ThreeLetterISORegionName,
            Title = regionInfo.EnglishName,
            NativeName = regionInfo.NativeName,
            CurrencyCode = regionInfo.ISOCurrencySymbol,
            CurrencySymbol = regionInfo.CurrencySymbol
        };

    public static IEnumerable<Country> ToCountries(this IEnumerable<CultureInfo> cultures)
        => cultures
            .Select(c => (culture: c, region: c.TryGetRegionInfo()))
            .Where(x => x.region != null)
            .GroupBy(x => x.region!.TwoLetterISORegionName)
            .Select(g =>
            {
                var firstItem = g.Any(x => x.culture.Name == CultureInfo.CurrentCulture.Name)
                    ? g.First(x => x.culture.Name == CultureInfo.CurrentCulture.Name)
                    : g.Any(x => x.culture.Name == CultureInfo.CurrentUICulture.Name)
                        ? g.First(x => x.culture.Name == CultureInfo.CurrentUICulture.Name)
                        : g.First();
                var firstRegion = firstItem.region!;

                // Build a dictionary of localized names for the region based on the cultures in the group
                var names = g
                    .GroupBy(x => x.culture.TwoLetterISOLanguageName)
                    .ToDictionary(lg => lg.Key, lg => lg.First().region!.NativeName);

                var region = firstRegion.ToCountry() with { Names = names };

                return region;
            });

    static RegionInfo? TryGetRegionInfo(this CultureInfo culture)
    {
        try { return new RegionInfo(culture.LCID); }
        catch { return null; }
    }
    static RegionInfo ToRegionInfo(this Country item)
    {
        try { return new RegionInfo(item.Iso2Code); }
        catch { throw new ArgumentException($"Invalid ISO 2-letter code: {item.Iso2Code}"); }
    }
}
