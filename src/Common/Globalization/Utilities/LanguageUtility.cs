using Regira.Globalization.Models;
using System.Globalization;

namespace Regira.Globalization.Utilities;

public static class LanguageUtility
{
    public static IEnumerable<Language> GetLanguages(CultureSearchObject? so = null)
        => CultureUtility.GetAll((so ?? new CultureSearchObject()) with { CultureTypes = CultureTypes.NeutralCultures })
            .Select(ToLanguage);
    public static Language? GetLanguage(string code)
        => CultureUtility.GetAll(new CultureSearchObject { CultureTypes = CultureTypes.NeutralCultures, LanguageCodes = [code] })
            .Select(ToLanguage)
            .FirstOrDefault();

    public static IDictionary<string, string> GetLanguageNames(string code, params string[] targetIso2Codes)
    {
        // 1. Create the culture we want to translate (e.g., English)
        var sourceCulture = CultureInfo.GetCultureInfo(code);

        // 2. Save the original UI culture to restore it later
        var originalUiCulture = CultureInfo.CurrentUICulture;

        var translations = new Dictionary<string, string>();
        try
        {
            // 3. Get cultures
            var so = new CultureSearchObject { CultureTypes = CultureTypes.NeutralCultures, LanguageCodes = targetIso2Codes };
            IEnumerable<CultureInfo> cultures = CultureUtility.GetAll(so);

            foreach (var targetCulture in cultures)
            {
                // 4. Temporarily set the UI culture to the target language
                CultureInfo.CurrentUICulture = targetCulture;

                // 5. Get the source culture's name as it appears in the target language
                // Use ToTitleCase to ensure names like "français" become "Français"
                string translatedName = targetCulture.TextInfo.ToTitleCase(sourceCulture.DisplayName);

                translations[targetCulture.Name] = translatedName;
            }
        }
        finally
        {
            // 6. Restore the original UI culture
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        return translations;
    }

    public static string? ToIso3Code(string iso2Code)
        => GetLanguage(iso2Code)?.Iso3Code;
    public static string? ToIso2Code(string iso3Code)
        => GetLanguage(iso3Code)?.Iso2Code;

    public static Language ToLanguage(this CultureInfo culture)
    {
        return new Language
        {
            Iso2Code = culture.TwoLetterISOLanguageName,
            Iso3Code = culture.ThreeLetterISOLanguageName,
            Title = culture.EnglishName,
            NativeName = culture.NativeName
        };
    }
}
