using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StringUtilityTests
{
    [TestCase("camelCase", "camel_Case")]
    [TestCase("hello world", "hello_world")]
    [TestCase("some-mixed_string With spaces_underscores-and-hyphens", "some_mixed_string_With_spaces_underscores_and_hyphens")]
    public void Test_SnakeCase(string input, string expected)
    {
        var output = input.ToSnakeCase();
        Assert.That(output, Is.EqualTo(expected));
    }

    [TestCase("camelCase", "CamelCase")]
    [TestCase("hello world", "HelloWorld")]
    [TestCase("some-mixed_string With spaces_underscores-and-hyphens", "SomeMixedStringWithSpacesUnderscoresAndHyphens")]
    public void Test_PascalCase(string input, string expected)
    {
        var output = input.ToPascalCase();
        Assert.That(output, Is.EqualTo(expected));
    }

    [TestCase("camelCase", "camelCase")]
    [TestCase("hello world", "helloWorld")]
    [TestCase("some-mixed_string With spaces_underscores-and-hyphens", "someMixedStringWithSpacesUnderscoresAndHyphens")]
    public void Test_CamelCase(string input, string expected)
    {
        var output = input.ToCamelCase();
        Assert.That(output, Is.EqualTo(expected));
    }

    [TestCase("camelCase", "camel-Case")]
    [TestCase("hello world", "hello-world")]
    [TestCase("some-mixed_string With spaces_underscores-and-hyphens", "some-mixed-string-With-spaces-underscores-and-hyphens")]
    public void Test_KebabCase(string input, string expected)
    {
        var output = input.ToKebabCase();
        Assert.That(output, Is.EqualTo(expected));
    }

    [TestCase("camelCase", "Camel Case")]
    [TestCase("hello world", "Hello World")]
    [TestCase("some-mixed_string With spaces_underscores-and-hyphens", "Some Mixed String With Spaces Underscores And Hyphens")]
    public void Test_ProperCase(string input, string expected)
    {
        var output = input.ToProperCase();
        Assert.That(output, Is.EqualTo(expected));
    }

    [TestCase("Cœur", "Coeur")]
    [TestCase("Crème brûlée", "Creme brulee")]
    [TestCase("Você está numa situação lamentável", "Voce esta numa situacao lamentavel")]
    public void Test_RemoveDiacritics(string input, string expected)
    {
        var output = input.RemoveDiacritics();
        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public void Inject_Fills_Keys_And_Formats()
    {
        var output = "{name} owes {amount:0.00}".Inject(new Dictionary<string, object?> { ["name"] = "Ann", ["amount"] = 12.5m }, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(output, Is.EqualTo("Ann owes 12.50"));
    }
    [Test]
    public void Inject_Matches_A_Key_Literally()
    {
        var values = new Dictionary<string, object?> { ["Customer.Name"] = "Ann", ["Total (incl.)"] = "10", ["Items[0]"] = "pen" };
        var output = "{Customer.Name} / {CustomerXName} / {Total (incl.)} / {Items[0]}".Inject(values);
        Assert.That(output, Is.EqualTo("Ann / {CustomerXName} / 10 / pen"));
    }
}
