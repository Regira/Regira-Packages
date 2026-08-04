using Regira.Dimensions;
using Regira.Utilities;
using System.Text.Json;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class DimensionsTests
{
    public class Dimensions
    {
        public float[] Mm { get; set; } = null!;
        public float[] Inches { get; set; } = null!;
        public float[] Pt72 { get; set; } = null!;
        public float[] Pt300 { get; set; } = null!;
    }

    private Dictionary<string, Dimensions> _references = null!;
    [SetUp]
    public void SetUp()
    {
        var reference = @"{
	""a0"":{""Mm"":[841,1189],""In"":[33.110237,46.811024],""Pt72"":[2383.937,3370.3938],""Pt300"":[9933.071,14043.307]},
	""a3"":{""Mm"":[297,420],""In"":[11.692914,16.535433],""Pt72"":[841.88983,1190.5511],""Pt300"":[3507.8743,4960.63]},
	""a4"":{""Mm"":[210,297],""In"":[8.267716,11.692914],""Pt72"":[595.2756,841.88983],""Pt300"":[2480.315,3507.8743]},
	""a7"":{""Mm"":[74,105],""In"":[2.9133859,4.133858],""Pt72"":[209.76378,297.6378],""Pt300"":[874.01575,1240.1575]}
}";
        _references = JsonSerializer.Deserialize<Dictionary<string, Dimensions>>(reference)!
            .ToDictionary(k => k.Key, v =>
            {
                v.Value.Inches = (Size2D)v.Value.Mm / DimensionsUtility.MM_PER_INCH;
                v.Value.Pt72 = (Size2D)v.Value.Mm / DimensionsUtility.MM_PER_INCH * 72;
                v.Value.Pt300 = (Size2D)v.Value.Mm / DimensionsUtility.MM_PER_INCH * 300;
                return v.Value;
            });
    }

    [TestCase("a0")]
    [TestCase("a3")]
    [TestCase("a4")]
    [TestCase("a7")]
    public void Size2D_To_Inches(string format)
    {
        var size = _references[format];
        var expected = ((Size2D)size.Inches).Round(2);
        Assert.That(DimensionsUtility.MmToIn(size.Mm).Round(2), Is.EqualTo(expected));
        Assert.That(DimensionsUtility.PtToIn(size.Pt72, 72).Round(2), Is.EqualTo(expected));
        Assert.That(DimensionsUtility.PtToIn(size.Pt300, 300).Round(2), Is.EqualTo(expected));
    }

    [TestCase("a0")]
    [TestCase("a3")]
    [TestCase("a4")]
    [TestCase("a7")]
    public void Size2D_To_Pt72(string format)
    {
        var size = _references[format];
        var expected = ((Size2D)size.Pt72).Round();
        Assert.That(DimensionsUtility.MmToPt(size.Mm, 72).Round(), Is.EqualTo(expected));
        Assert.That(DimensionsUtility.InToPt(size.Inches, 72).Round(), Is.EqualTo(expected));
    }

    [TestCase("a0")]
    [TestCase("a3")]
    [TestCase("a4")]
    [TestCase("a7")]
    public void Size2D_To_Pt300(string format)
    {
        var size = _references[format];
        var expected = ((Size2D)size.Pt300).Round();
        Assert.That(DimensionsUtility.MmToPt(size.Mm, 300).Round(), Is.EqualTo(expected));
        Assert.That(DimensionsUtility.InToPt(size.Inches, 300).Round(), Is.EqualTo(expected));
    }

    [TestCase("a0")]
    [TestCase("a3")]
    [TestCase("a4")]
    [TestCase("a7")]
    public void Size2D_To_Mm(string format)
    {
        var size = _references[format];
        var expected = ((Size2D)size.Mm).Round(2);
        Assert.That(DimensionsUtility.InToMm(size.Inches).Round(2), Is.EqualTo(expected));
        Assert.That(DimensionsUtility.PtToMm(size.Pt72, 72).Round(2), Is.EqualTo(expected));
        Assert.That(DimensionsUtility.PtToMm(size.Pt300, 300).Round(2), Is.EqualTo(expected));
    }

    [TestCase("a0")]
    [TestCase("a3")]
    [TestCase("a4")]
    [TestCase("a7")]
    public void Modify_DPI_To_72(string format)
    {
        var size = _references[format];
        var expected = ((Size2D)size.Pt72).Round();
        Assert.That(DimensionsUtility.ModifyDpi(size.Pt300, 300, 72).Round(), Is.EqualTo(expected));
    }

    [TestCase("a0")]
    [TestCase("a3")]
    [TestCase("a4")]
    [TestCase("a7")]
    public void Modify_DPI_To_300(string format)
    {
        var size = _references[format];
        var expected = ((Size2D)size.Pt300).Round();
        Assert.That(DimensionsUtility.ModifyDpi(size.Pt72, 72, 300).Round(), Is.EqualTo(expected));
    }
}