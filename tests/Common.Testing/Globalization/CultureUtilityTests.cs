using Regira.Globalization.Models;
using Regira.Globalization.Utilities;
using System.Globalization;

namespace Common.Testing.Globalization;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CultureUtilityTests
{
    [Test]
    public void List_No_Filter_Returns_Cultures()
    {
        var cultures = CultureUtility.GetAll().ToArray();
        Assert.That(cultures, Is.Not.Empty);
    }

    [Test]
    public void List_Excludes_Custom_Cultures_By_Default()
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject()).ToArray();
        Assert.That(cultures.All(c => c.LCID != 4096), Is.True);
    }

    [Test]
    public void List_Excludes_Invariant_Culture_By_Default()
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject()).ToArray();
        Assert.That(cultures.All(c => c.LCID != 127), Is.True);
    }

    [TestCase("nl", new[] { "nl-BE", "nl-NL" })]
    [TestCase("fr", new[] { "fr-BE", "fr-FR", "fr-CH" })]
    public void List_LanguageCodes_Filter_Includes_Expected(string lang, string[] expectedNames)
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            LanguageCodes = [lang],
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        var names = cultures.Select(c => c.Name).ToArray();
        foreach (var expected in expectedNames)
            Assert.That(names, Does.Contain(expected), $"Expected culture '{expected}' not found");
    }

    [TestCase("zh", "nl-BE")]
    [TestCase("nl", "zh-CN")]
    public void List_LanguageCodes_Filter_Excludes_Other(string lang, string excludedName)
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            LanguageCodes = [lang],
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        Assert.That(cultures.Select(c => c.Name), Does.Not.Contain(excludedName));
    }

    [TestCase("BE", new[] { "nl-BE", "fr-BE" })]
    [TestCase("US", new[] { "en-US" })]
    public void List_CountryCodes_Filter_Includes_Expected(string countryCode, string[] expectedNames)
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            CountryCodes = [countryCode],
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        var names = cultures.Select(c => c.Name).ToArray();
        foreach (var expected in expectedNames)
            Assert.That(names, Does.Contain(expected), $"Expected culture '{expected}' not found");
    }

    [TestCase("Belgium", new[] { "nl-BE", "fr-BE" })]
    [TestCase("Dutch", new[] { "nl-BE", "nl-NL" })]
    [TestCase("Belg", new[] { "nl-BE", "fr-BE" })]
    public void List_Q_Filter_Includes_Expected(string q, string[] expectedNames)
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            Q = q,
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        var names = cultures.Select(c => c.Name).ToArray();
        foreach (var expected in expectedNames)
            Assert.That(names, Does.Contain(expected), $"Q='{q}' expected to include '{expected}'");
    }

    [TestCase("Dutch", new[] { "nl-BE", "nl-NL" })]
    [TestCase("neder", new[] { "nl-BE", "nl-NL" })]
    public void List_LanguageQ_Filter_Includes_Expected(string q, string[] expectedNames)
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            LanguageQ = q,
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        var names = cultures.Select(c => c.Name).ToArray();
        foreach (var expected in expectedNames)
            Assert.That(names, Does.Contain(expected), $"LanguageQ='{q}' expected to include '{expected}'");
    }

    [TestCase("Belgi", new[] { "nl-BE", "fr-BE" })]
    [TestCase("United States", new[] { "en-US" })]
    public void List_CountryQ_Filter_Includes_Expected(string q, string[] expectedNames)
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            CountryQ = q,
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        var names = cultures.Select(c => c.Name).ToArray();
        foreach (var expected in expectedNames)
            Assert.That(names, Does.Contain(expected), $"CountryQ='{q}' expected to include '{expected}'");
    }

    [Test]
    public void List_SpecificCultures_Only()
    {
        var cultures = CultureUtility.GetAll(new CultureSearchObject
        {
            CultureTypes = CultureTypes.SpecificCultures,
            IsCustomCulture = false,
            IsInvariantCulture = false
        }).ToArray();

        Assert.That(cultures.All(c => c.Name.Contains('-')), Is.True);
    }
}
