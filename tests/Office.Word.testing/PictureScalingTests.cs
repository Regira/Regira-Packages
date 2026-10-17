using Regira.Office.Word.Layout;

namespace Office.Word.testing;

/// <summary>
/// The rule every Word backend scales a picture by when page settings change the text width
/// (<see cref="Regira.Office.Word.Models.ConversionOptions.AutoScalePictures"/>). In points.
/// </summary>
[TestFixture]
public class PictureScalingTests
{
    [Test]
    public void A_Picture_Scales_With_The_Text_Width()
        => Assert.That(PictureScaling.Factor(400, 600, 200, 100), Is.EqualTo(1.5));

    [Test]
    public void A_Picture_Stops_At_The_Largest_Shape_Word_Holds()
        => Assert.That(PictureScaling.Factor(400, 4000, 792, 100), Is.EqualTo(2), "1584 pt wide");

    /// <summary>
    /// A side the factor stops at the limit lands on it, never a rounding error past, which Spire.Doc and Aspose.Words
    /// throw for: the plain product passes it for about one width in twenty, in the float of Word.Spire and Word.Syncfusion
    /// and in the double of Word.Aspose. Every width from 1 to 1584 pt, in hundredths, grown fivefold.
    /// </summary>
    [Test]
    public void A_Side_Stopped_At_The_Limit_Is_Not_Past_It()
    {
        int productsPast = 0, sizesPast = 0;
        for (var hundredths = 100; hundredths <= 158400; hundredths++)
        {
            var width = hundredths / 100f;
            var factor = PictureScaling.Factor(400, 2000, width, 1);
            if (width * (float)factor > 1584f || (double)width * factor > PictureScaling.MaxShapeSize)
            {
                productsPast++;
            }
            var (size, _) = PictureScaling.Size(width, 1, factor);
            if ((float)size > 1584f || size > PictureScaling.MaxShapeSize)
            {
                sizesPast++;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(productsPast, Is.GreaterThan(1000), "the plain product");
            Assert.That(sizesPast, Is.Zero);
        });
    }

    /// <summary>
    /// Scaled by the text width alone, a picture past the limit stays past it, which Spire.Doc and Aspose.Words throw for.
    /// </summary>
    [TestCase(400, 404, TestName = "A_Picture_Past_The_Limit_Is_Brought_To_It_As_The_Text_Width_Grows")]
    [TestCase(400, 396, TestName = "A_Picture_Past_The_Limit_Is_Brought_To_It_As_The_Text_Width_Narrows")]
    public void A_Picture_Past_The_Limit_Is_Brought_To_It(double originalTextWidth, double newTextWidth)
        => Assert.That(PictureScaling.Factor(originalTextWidth, newTextWidth, 2000, 100), Is.EqualTo(0.792), "1584 pt wide");

    /// <summary>A picture already past the limit keeps its size too, as in a backend that does not scale it at all.</summary>
    [TestCase(0, 600, TestName = "A_Text_Width_That_Is_Unknown_Scales_Nothing")]
    [TestCase(400, 0, TestName = "A_Text_Width_That_Ends_Up_Unknown_Scales_Nothing")]
    [TestCase(400, 400, TestName = "A_Text_Width_That_Stays_As_It_Was_Scales_Nothing")]
    public void A_Text_Width_That_Is_Unknown_Or_Unchanged_Scales_Nothing(double originalTextWidth, double newTextWidth)
        => Assert.That(PictureScaling.Factor(originalTextWidth, newTextWidth, 2000, 100), Is.EqualTo(1));
}
