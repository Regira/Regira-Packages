using Regira.Globalization.Models;
using Regira.Globalization.Utilities;

namespace Common.Testing.Globalization;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CountryUtilityTests
{
    [Test]
    public void GetAllRegions_Returns_Non_Empty_Unique_List()
    {
        var regions = CountryUtility.GetCountries().ToList();
        Assert.That(regions, Is.Not.Empty);
        Assert.That(regions.Select(r => r.Iso2Code).Distinct().Count(), Is.EqualTo(regions.Count));
    }

    [Test]
    public void GetAllRegions_All_Have_Iso2Code_And_Title()
    {
        var regions = CountryUtility.GetCountries();
        Assert.That(regions.All(r => !string.IsNullOrEmpty(r.Iso2Code) && !string.IsNullOrEmpty(r.Title)), Is.True);
    }

    [Test]
    public void GetAllRegions_Have_Names_Dictionary()
    {
        var regions = CountryUtility.GetCountries();
        Assert.That(regions.All(r => r.Names.Any()), Is.True);
    }

    [TestCase("BE", "Belgium")]
    [TestCase("US", "United States")]
    [TestCase("DE", "Germany")]
    [TestCase("FR", "France")]
    public void GetRegion_By_Iso2Code(string code, string expectedTitle)
    {
        var region = CountryUtility.GetCountry(code);
        Assert.That(region, Is.Not.Null);
        Assert.That(region!.Title, Is.EqualTo(expectedTitle));
        Assert.That(region.Iso2Code, Is.EqualTo(code));
    }

    [TestCase("XX")]
    [TestCase("ZZZ")]
    public void GetRegion_Unknown_Code_Returns_Null(string code)
    {
        var region = CountryUtility.GetCountry(code);
        Assert.That(region, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void GetRegion_Null_Or_Empty_Returns_Null(string? code)
    {
        var region = CountryUtility.GetCountry(code);
        Assert.That(region, Is.Null);
    }

    [Test]
    public void GetRegion_Has_Currency_Info()
    {
        var region = CountryUtility.GetCountry("BE");
        Assert.That(region, Is.Not.Null);
        Assert.That(region!.CurrencyCode, Is.EqualTo("EUR"));
        Assert.That(region.CurrencySymbol, Is.EqualTo("€"));
    }

    [Test]
    public void GetRegion_Has_Iso3Code()
    {
        var region = CountryUtility.GetCountry("BE");
        Assert.That(region!.Iso3Code, Is.EqualTo("BEL"));
    }

    [TestCase("BE", new[] { "nl", "fr" })]
    [TestCase("CH", new[] { "de", "fr", "it" })]
    [TestCase("DE", new[] { "de" })]
    [TestCase("NL", new[] { "nl" })]
    [TestCase("LU", new[] { "de", "fr" })]
    [TestCase("CA", new[] { "en", "fr" })]
    public void GetLanguages_Returns_Expected_Languages(string countryCode, string[] expectedLanguages)
    {
        var country = CountryUtility.GetCountries().FirstOrDefault(c => c.Iso2Code == countryCode);
        Assert.That(country, Is.Not.Null, $"Country '{countryCode}' not found");
        var languages = country!.GetLanguages();

        foreach (var lang in expectedLanguages)
            Assert.That(languages, Does.Contain(lang), $"Expected language '{lang}' not found for country '{countryCode}'");
    }

    [TestCase("nl", new[] { "NL", "BE" })]
    [TestCase("de", new[] { "DE", "AT", "CH", "LI", "LU" })]
    [TestCase("xx", new string[0])]
    public void Test_Get_By_Language(string lang, string[] expectedCodes)
    {
        var regions = CountryUtility.GetCountries(new CultureSearchObject { LanguageCodes = [lang] }).ToArray();
        var codes = regions.Select(r => r.Iso2Code).ToArray();
        foreach (var expected in expectedCodes)
            Assert.That(codes, Does.Contain(expected), $"Expected region '{expected}' not found for language '{lang}'");
    }

    // GetName with a known language code returns the localized native name from Names dictionary
    [TestCase("BE", "nl", "België")]
    [TestCase("BE", "fr", "Belgique")]
    [TestCase("DE", "de", "Deutschland")]
    [TestCase("FR", "fr", "France")]
    [TestCase("US", "en", "United States")]
    // GetName with an unknown language code falls back to the English Title
    [TestCase("BE", "xx", "Belgium")]
    [TestCase("DE", "xx", "Germany")]
    public void GetName_Returns_Localized_Or_Fallback(string countryCode, string langCode, string expectedName)
    {
        var country = CountryUtility.GetCountry(countryCode);
        Assert.That(country, Is.Not.Null);
        Assert.That(country!.GetName(langCode), Is.EqualTo(expectedName));
    }

    [Test]
    public void GetName_No_Language_Returns_DisplayName()
    {
        var country = CountryUtility.GetCountry("BE");
        Assert.That(country, Is.Not.Null);
        Assert.That(country!.GetName(), Is.Not.Null.And.Not.Empty);
    }
}
