using Regira.Office.OCR.Tesseract;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.OCR.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TesseractTests
{
    private readonly string _assetsDir;
    public TesseractTests()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "../../../Assets");
        _assetsDir = new DirectoryInfo(assetsDir).FullName;
    }

    [Test]
    public Task Test_With_Defaults() => new OcrManager().Test_Read_EN(_assetsDir);

    [Test]
    public Task Read_English()
    {
        var mgr = new OcrManager(new OcrManager.Options { Language = "en" });
        return mgr.Test_Read_EN(_assetsDir);
    }
    [Test]
    public Task Read_Dutch()
    {
        var mgr = new OcrManager(new OcrManager.Options { Language = "nl" });
        return mgr.Test_Read_NL(_assetsDir);
    }
}
