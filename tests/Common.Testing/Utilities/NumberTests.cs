using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NumberTests
{
    [TestCase(1, "I")]
    [TestCase(5, "V")]
    [TestCase(10, "X")]
    [TestCase(2, "ii")]
    [TestCase(14, "XIV")]
    [TestCase(9, "ix")]
    [TestCase(119, "cxix")]
    [TestCase(449, "CDXLIX")]
    [TestCase(999, "CMXCIX")]
    [TestCase(1556, "mdlvi")]
    public void FromRoman(int expected, string roman)
    {
        var result = NumberUtility.FromRomanNumeral(roman);
        Assert.That(expected, Is.EqualTo(result));
    }

    [TestCase(1, "I")]
    [TestCase(5, "V")]
    [TestCase(10, "X")]
    [TestCase(2, "II")]
    [TestCase(14, "XIV")]
    [TestCase(9, "IX")]
    [TestCase(119, "CXIX")]
    [TestCase(449, "CDXLIX")]
    [TestCase(999, "CMXCIX")]
    [TestCase(1556, "MDLVI")]
    public void ToRoman(int number, string expected)
    {
        var result = NumberUtility.ToRomanNumeral(number);
        Assert.That(expected, Is.EqualTo(result));
    }
}