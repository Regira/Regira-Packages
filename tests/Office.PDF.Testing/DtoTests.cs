using Regira.Office.PDF.Models;
using Regira.Office.PDF.Models.DTO;
using Regira.Utilities;

namespace Office.PDF.Testing;

/// <summary>The DTO <c>PdfClient</c> sends, read back on the server.</summary>
public class DtoTests
{
    // the DTO's heights are plain ints: a missing height must come back missing, or a backend draws a 0 cm band
    [Test]
    public void A_Header_Without_A_Height_Keeps_The_Default_Band()
    {
        var input = new HtmlInput { HtmlContent = "<p>Body</p>", HeaderHtmlContent = "<p>Head</p>", FooterHtmlContent = "<p>Foot</p>" };

        var read = input.ToHtmlToPdfInput().ToHtmlInput();

        Assert.That((read.HeaderHeight, read.FooterHeight), Is.EqualTo(((int?)null, (int?)null)));
    }

    [Test]
    public void A_Header_Height_Travels()
    {
        var input = new HtmlInput { HtmlContent = "<p>Body</p>", HeaderHtmlContent = "<p>Head</p>", HeaderHeight = 60, FooterHeight = 30 };

        var read = input.ToHtmlToPdfInput().ToHtmlInput();

        Assert.That((read.HeaderHeight, read.FooterHeight), Is.EqualTo(((int?)60, (int?)30)));
    }

    // the DTO measures margins at 96 DPI, so margins set at another DPI keep their size on the way
    [Test]
    public void Margins_Keep_Their_Size()
    {
        var input = new HtmlInput(72) { HtmlContent = "<p>Body</p>", Margins = 28.35f }; // 10 mm in points

        var read = input.ToHtmlToPdfInput().ToHtmlInput();

        Assert.That(read.Margins.Top, Is.EqualTo(DimensionsUtility.MmToPt(10f)).Within(0.01f));
    }

    // a request that sends no margins gets HtmlInput's 10 mm
    [Test]
    public void A_Dto_Without_Margins_Reads_As_10_mm()
    {
        var read = new HtmlToPdfInputDto { HtmlContent = "<p>Body</p>" }.ToHtmlInput();

        Assert.That(read.Margins, Is.EqualTo(new HtmlInput().Margins));
    }
}
