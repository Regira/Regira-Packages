using Regira.Entities.Keywords;

namespace Entities.Testing;

// QKeyword carries the same keyword twice: once raw (the Trimmed* family) and once normalized (the
// unprefixed members). Pairing a family with the wrong column compiles and silently matches nothing, so
// the split is a contract, not an implementation detail — these cases pin which member holds which.
[TestFixture]
public class QKeywordFamilyTests
{
    // normalizing, exactly as DI registers it: the default normalizer upper-cases, drops '.' and turns
    // '-' into a space, so raw and normalized are visibly different for this input
    private readonly QKeywordHelper _qHelper = new();

    [Test]
    public void Trimmed_Family_Carries_The_Raw_Keyword()
    {
        var kw = _qHelper.ParseKeyword("my-report.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(kw.Trimmed, Is.EqualTo("my-report.pdf"));
            Assert.That(kw.TrimmedStartsWith, Is.EqualTo("my-report.pdf%"));
            Assert.That(kw.TrimmedEndsWith, Is.EqualTo("%my-report.pdf"));
            Assert.That(kw.TrimmedQW, Is.EqualTo("%my-report.pdf%"));
        });
    }

    [Test]
    public void Unprefixed_Family_Carries_The_Normalized_Keyword()
    {
        var kw = _qHelper.ParseKeyword("my-report.pdf");
        var normalized = kw.Normalized;

        // whatever the normalizer does to it, every unprefixed member is built from that value — and for
        // this input it is NOT the raw one, which is the whole reason the two families exist
        Assert.Multiple(() =>
        {
            Assert.That(normalized, Is.Not.EqualTo(kw.Trimmed));
            Assert.That(kw.StartsWith, Is.EqualTo($"{normalized}%"));
            Assert.That(kw.EndsWith, Is.EqualTo($"%{normalized}"));
            Assert.That(kw.QW, Is.EqualTo($"%{normalized}%"));
        });
    }

    // Q and TrimmedQ apply the wildcards the INPUT carried, unlike the QW/StartsWith/EndsWith forms which
    // always place their own.
    [TestCase("report", "report", "report")]
    [TestCase("report*", "report", "report%")]
    [TestCase("*report", "report", "%report")]
    [TestCase("*report*", "report", "%report%")]
    public void TrimmedQ_Mirrors_The_Input_Wildcards(string input, string expectedTrimmed, string expectedQ)
    {
        var kw = _qHelper.ParseKeyword(input);

        Assert.Multiple(() =>
        {
            Assert.That(kw.Keyword, Is.EqualTo(input));
            Assert.That(kw.Trimmed, Is.EqualTo(expectedTrimmed));
            Assert.That(kw.TrimmedQ, Is.EqualTo(expectedQ));
        });
    }

    [TestCase("report", false, false)]
    [TestCase("report*", false, true)]
    [TestCase("*report", true, false)]
    [TestCase("*report*", true, true)]
    public void Wildcard_Flags_Report_Which_End_The_Input_Marked(string input, bool atStart, bool atEnd)
    {
        var kw = _qHelper.ParseKeyword(input);

        Assert.Multiple(() =>
        {
            Assert.That(kw.HasWildcardAtStart, Is.EqualTo(atStart));
            Assert.That(kw.HasWildcardAtEnd, Is.EqualTo(atEnd));
            Assert.That(kw.HasWildcard, Is.EqualTo(atStart || atEnd));
        });
    }

    [Test]
    public void Without_Normalizing_Both_Families_Agree()
    {
        var helper = new QKeywordHelper(new QKeywordHelperOptions { ApplyNormalize = false });

        var kw = helper.ParseKeyword("my-report.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(kw.Normalized, Is.EqualTo(kw.Trimmed));
            Assert.That(kw.StartsWith, Is.EqualTo(kw.TrimmedStartsWith));
            Assert.That(kw.EndsWith, Is.EqualTo(kw.TrimmedEndsWith));
            Assert.That(kw.Q, Is.EqualTo(kw.TrimmedQ));
            Assert.That(kw.QW, Is.EqualTo(kw.TrimmedQW));
        });
    }
}
