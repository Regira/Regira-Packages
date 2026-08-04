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
    public Task Read_Dutch() => new OcrManager().Test_Read_NL(_assetsDir);
}