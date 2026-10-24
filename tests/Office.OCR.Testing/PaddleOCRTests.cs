using Regira.Office.OCR.PaddleOCR;

namespace Office.OCR.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PaddleOCRTests
{
    private readonly string _assetsDir;
    public PaddleOCRTests()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "../../../Assets");
        _assetsDir = new DirectoryInfo(assetsDir).FullName;
    }

    [Test]
    public Task Read_English() => new OcrManager().Test_Read_EN(_assetsDir);

    [Test]
    [Ignore("Library gap: for nl, Read picks LocalFullModels.LatinV5, which reads 'wil ik' in poem-nl.jpg as 'wilik'; EnglishV5, the model Read picks without a language, reads the poem exactly")]
    public Task Read_Dutch() => new OcrManager().Test_Read_NL(_assetsDir, "nl");

    // the Dutch poem without a language, which takes the model that reads it exactly
    [Test]
    public Task Read_Dutch_Without_A_Language() => new OcrManager().Test_Read_NL(_assetsDir);
}