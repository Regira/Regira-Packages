using Regira.Media.Drawing.Models;

namespace Drawing.Testing;

[TestFixture]
public class ColorTests
{
    [TestCase(null, "#FFFFFFFF")]
    [TestCase("#FFF", "#FFFFFFFF")]
    [TestCase("#FFF0", "#FFFFFF00")]
    [TestCase("#FFFF", "#FFFFFFFF")]
    [TestCase("#FFFFFF", "#FFFFFFFF")]
    [TestCase("#FFFFFFFF", "#FFFFFFFF")]
    [TestCase("#FFFFFF00", "#FFFFFF00")]
    [TestCase("FFFFFF00", "#FFFFFF00")]
    [TestCase("#000", "#000000FF")]
    [TestCase("#0000", "#00000000")]
    [TestCase("#000F", "#000000FF")]
    [TestCase("#000000", "#000000FF")]
    [TestCase("#000000FF", "#000000FF")]
    [TestCase("#00000000", "#00000000")]
    [TestCase("00000000", "#00000000")]
    public void ParseColor(string? inputHex, string expected)
    {
        var color = (Color)inputHex;
        Assert.That(color.ToString(), Is.EqualTo(expected), $"Expected color {expected} for input '{inputHex}'");
    }

    [Test]
    public void TestTransparent()
    {
        var color = Color.Transparent;
        Assert.That(color.Hex, Is.EqualTo("#FFFFFF"));
        Assert.That(color.ToString(), Is.EqualTo("#FFFFFF00"));
        Assert.That(color.Alpha, Is.EqualTo(byte.MinValue));
    }
    [Test]
    public void TestWhite()
    {
        var color = Color.White;
        Assert.That(color.Hex, Is.EqualTo("#FFFFFF"));
        Assert.That(color.ToString(), Is.EqualTo("#FFFFFFFF"));
        Assert.That(color.Alpha, Is.EqualTo(byte.MaxValue));
        Assert.That(color.Red, Is.EqualTo(byte.MaxValue));
        Assert.That(color.Green, Is.EqualTo(byte.MaxValue));
        Assert.That(color.Blue, Is.EqualTo(byte.MaxValue));
    }
    [Test]
    public void TestBlack()
    {
        var color = Color.Black;
        Assert.That(color.Hex, Is.EqualTo("#000000"));
        Assert.That(color.ToString(), Is.EqualTo("#000000FF"));
        Assert.That(color.Alpha, Is.EqualTo(byte.MaxValue));
        Assert.That(color.Red, Is.EqualTo(byte.MinValue));
        Assert.That(color.Green, Is.EqualTo(byte.MinValue));
        Assert.That(color.Blue, Is.EqualTo(byte.MinValue));
    }
}