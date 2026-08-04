using Regira.Globalization.Utilities;

namespace Common.Testing.Globalization;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LanguageUtilityTests
{
    [Test]
    public void GetAllLanguages_Returns_Non_Empty_List()
    {
        var languages = LanguageUtility.GetLanguages().ToArray();
        Assert.That(languages, Is.Not.Empty);
    }

    [Test]
    public void GetAllLanguages_All_Have_Required_Fields()
    {
        var languages = LanguageUtility.GetLanguages().ToArray();
        Assert.That(languages.All(l =>
            !string.IsNullOrEmpty(l.Iso2Code) &&
            !string.IsNullOrEmpty(l.Iso3Code) &&
            !string.IsNullOrEmpty(l.Title) &&
            !string.IsNullOrEmpty(l.NativeName)
        ), Is.True);
    }

    [TestCase("nl", "Dutch", "Nederlands")]
    [TestCase("fr", "French", "français")]
    [TestCase("de", "German", "Deutsch")]
    [TestCase("en", "English", "English")]
    public void GetLanguage_By_Iso2Code(string code, string expectedTitle, string expectedNativeName)
    {
        var language = LanguageUtility.GetLanguage(code);
        Assert.That(language, Is.Not.Null);
        Assert.That(language!.Iso2Code, Is.EqualTo(code));
        Assert.That(language.Title, Is.EqualTo(expectedTitle));
        Assert.That(language.NativeName, Is.EqualTo(expectedNativeName));
    }

    [TestCase("nld", "nl")]
    [TestCase("fra", "fr")]
    [TestCase("deu", "de")]
    public void GetLanguage_By_Iso3Code(string iso3, string expectedIso2)
    {
        var language = LanguageUtility.GetLanguage(iso3);
        Assert.That(language, Is.Not.Null);
        Assert.That(language!.Iso2Code, Is.EqualTo(expectedIso2));
    }

    [TestCase("xx")]
    [TestCase("zzz")]
    public void GetLanguage_Unknown_Code_Returns_Null(string code)
    {
        var language = LanguageUtility.GetLanguage(code);
        Assert.That(language, Is.Null);
    }

    [TestCase("nl", "nld")]
    [TestCase("fr", "fra")]
    [TestCase("de", "deu")]
    public void ToIso3Code(string iso2, string expectedIso3)
    {
        var iso3 = LanguageUtility.ToIso3Code(iso2);
        Assert.That(iso3, Is.EqualTo(expectedIso3));
    }

    [TestCase("nld", "nl")]
    [TestCase("fra", "fr")]
    [TestCase("deu", "de")]
    public void ToIso2Code(string iso3, string expectedIso2)
    {
        var iso2 = LanguageUtility.ToIso2Code(iso3);
        Assert.That(iso2, Is.EqualTo(expectedIso2));
    }

    [TestCase("xx")]
    public void ToIso3Code_Unknown_Returns_Null(string iso2)
    {
        Assert.That(LanguageUtility.ToIso3Code(iso2), Is.Null);
    }

    [TestCase("zzz")]
    public void ToIso2Code_Unknown_Returns_Null(string iso3)
    {
        Assert.That(LanguageUtility.ToIso2Code(iso3), Is.Null);
    }

    [Test]
    public void GetLanguageNames_Returns_Translations()
    {
        var names = LanguageUtility.GetLanguageNames("en", "nl", "fr", "de");
        Assert.That(names, Is.Not.Empty);
        Assert.That(names.Keys, Is.EquivalentTo(new[] { "nl", "fr", "de" }));
    }

    [Test]
    public void GetLanguageNames_Returns_Correct_Translations_For_English()
    {
        var names = LanguageUtility.GetLanguageNames("en", "nl", "fr");
        Assert.That(names["nl"], Is.EqualTo("Engels"));
        Assert.That(names["fr"], Is.EqualTo("Anglais"));
    }

    [Test]
    public void GetLanguageNames_No_Target_Codes_Returns_All()
    {
        var names = LanguageUtility.GetLanguageNames("en");
        Assert.That(names, Is.Not.Empty);
    }
}
