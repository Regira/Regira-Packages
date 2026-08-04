using Regira.Office.OCR.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace Office.OCR.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PaddleOCRLanguageTests
{
    [TestCase(null, "en_PP-OCRv5_mobile_rec")]
    [TestCase("", "en_PP-OCRv5_mobile_rec")]
    [TestCase("en", "en_PP-OCRv5_mobile_rec")]
    [TestCase("xx", "en_PP-OCRv5_mobile_rec")]
    [TestCase("nl", "latin_PP-OCRv5_mobile_rec")]
    [TestCase("FR", "latin_PP-OCRv5_mobile_rec")]
    [TestCase("zh", "mobile-zh-rec")]
    [TestCase("cn", "mobile-zh-rec")]
    [TestCase("ru", "eslav_PP-OCRv5_mobile_rec")]
    public void ConvertLang_Returns_Expected_Model(string? lang, string expectedName)
    {
        var model = new OcrManager().ConvertLang(lang);
        Assert.That(((LocalRecognizationModel)model.RecognizationModel).Name, Is.EqualTo(expectedName));
    }
}
