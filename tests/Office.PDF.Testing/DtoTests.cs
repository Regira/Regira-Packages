using Regira.Office.PDF.Models;
using Regira.Office.PDF.Models.DTO;

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
}
