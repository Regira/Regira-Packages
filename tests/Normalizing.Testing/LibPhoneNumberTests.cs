using System.Globalization;
using Regira.Globalization.LibPhoneNumber;

[assembly: Parallelizable(ParallelScope.Fixtures)]
namespace Normalizing.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LibPhoneNumberTests
{
    private readonly PhoneNumberFormatter _formatter = new(new CultureInfo("nl-BE"));

    [TestCase("+32 486 99 99 99", "+32486999999")]
    [TestCase("0486999999", "+32486999999")]
    [TestCase("0486 99 99 99", "+32486999999")]
    [TestCase("0486/99.99.99", "+32486999999")]
    [TestCase("033843044", "+3233843044")]
    [TestCase("03 384 30 44", "+3233843044")]
    [TestCase("03/384.30.44", "+3233843044")]
    [TestCase("0800 10 000", "+3280010000")]
    [TestCase("070 10 10 10", "+3270101010")]
    public void TestParse(string input, string expected)
    {
        var output = _formatter.Normalize(input);

        Assert.That(output, Is.EqualTo(expected));
    }

    [TestCase("+32 486 99 99 99", "+32 486 99 99 99")]
    [TestCase("0486 99 99 99", "+32 486 99 99 99")]
    [TestCase("0486/99.99.99", "+32 486 99 99 99")]
    [TestCase("033843044", "+32 3 384 30 44")]
    [TestCase("03 384 30 44", "+32 3 384 30 44")]
    [TestCase("03/384.30.44", "+32 3 384 30 44")]
    public void TestFormat(string input, string expected)
    {
        var output = _formatter.Format(input);

        Assert.That(output, Is.EqualTo(expected));
    }
}