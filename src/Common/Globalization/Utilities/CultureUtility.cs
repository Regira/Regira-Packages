using Regira.Globalization.Models;
using System.Globalization;

namespace Regira.Globalization.Utilities;

public static class CultureUtility
{
    public static IEnumerable<CultureInfo> GetAll(CultureSearchObject? so = null)
    {
        IEnumerable<CultureInfo> items = CultureInfo.GetCultures(so?.CultureTypes ?? CultureTypes.AllCultures);

        if (so == null)
        {
            return items;
        }

        // Custom cultures
        if (so.IsCustomCulture.HasValue)
        {
            items = items
                .Where(c => so.IsCustomCulture == (c.LCID == 4096));
        }
        // Invariant culture
        if (so.IsInvariantCulture.HasValue)
        {
            items = items
                .Where(c => so.IsInvariantCulture == (c.LCID == 127));
        }

        // Language codes
        if (so.LanguageCodes?.Any() == true)
        {
            items = items
                .Where(c => so.LanguageCodes.Any(code =>
                    c.TwoLetterISOLanguageName.Equals(code, StringComparison.OrdinalIgnoreCase)
                    || c.ThreeLetterISOLanguageName.Equals(code, StringComparison.OrdinalIgnoreCase)
                ));
        }
        // Region codes
        if (so.CountryCodes?.Any() == true)
        {
            items = items
                .Where(c => so.CountryCodes.Any(code => c.Name.EndsWith($"-{code}", StringComparison.OrdinalIgnoreCase)));
        }

        // Q globally
        if (!string.IsNullOrWhiteSpace(so.Q))
        {
            items = items
                .Where(c => MatchesLanguageQ(c, so.Q) || MatchesCountryQ(c, so.Q));
        }
        // Language Q
        if (!string.IsNullOrWhiteSpace(so.LanguageQ))
        {
            items = items
                .Where(c => MatchesLanguageQ(c, so.LanguageQ));
        }
        // Region Q
        if (!string.IsNullOrWhiteSpace(so.CountryQ))
        {
            items = items
                .Where(c => MatchesCountryQ(c, so.CountryQ));
        }

        return items;
    }

    static bool MatchesLanguageQ(CultureInfo c, string q)
    {
        return ContainsIgnoreCase(c.TwoLetterISOLanguageName, q)
            || ContainsIgnoreCase(c.ThreeLetterISOLanguageName, q)
            || ContainsIgnoreCase(c.DisplayName, q)
            || ContainsIgnoreCase(c.NativeName, q);
    }
    static bool MatchesCountryQ(CultureInfo c, string q)
    {
        var region = new RegionInfo(c.LCID);
        return ContainsIgnoreCase(region.TwoLetterISORegionName, q)
            || ContainsIgnoreCase(region.ThreeLetterISORegionName, q)
            || ContainsIgnoreCase(region.Name, $"-{q}")
            || ContainsIgnoreCase(region.DisplayName, q)
            || ContainsIgnoreCase(region.NativeName, q);
    }

    static bool ContainsIgnoreCase(string source, string value)
        => source.Contains(value, StringComparison.OrdinalIgnoreCase);
}