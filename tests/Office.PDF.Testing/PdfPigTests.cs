using Office.PDF.Testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.Office.PDF.PdfPig;

namespace Office.PDF.Testing;

/// <summary>PDF.PdfPig runs every shared scenario in <see cref="PdfTestsBase{TBackend}"/> as it stands.</summary>
[PdfFixture]
public class PdfPigTests() : PdfTestsBase<PdfService>(new PdfService(new ImageService()), "PdfPig")
{
}
