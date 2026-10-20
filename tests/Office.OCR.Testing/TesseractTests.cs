using Regira.Office.OCR.Tesseract;

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
    [NonParallelizable]
    public async Task Defaults_Read_From_The_Output_Folder_Whatever_The_Working_Directory()
    {
        // dotnet run and Visual Studio start an app in its project folder, not in its output folder
        var workingDirectory = Directory.GetCurrentDirectory();
        var elsewhere = Directory.CreateTempSubdirectory("regira-ocr-");
        Directory.SetCurrentDirectory(elsewhere.FullName);
        try
        {
            await new OcrManager().Test_Read_EN(_assetsDir);
        }
        finally
        {
            Directory.SetCurrentDirectory(workingDirectory);
            elsewhere.Delete();
        }
    }

    [TestCase("")]
    [TestCase(null)]
    [NonParallelizable]
    public async Task An_Empty_DataDirectory_Leaves_The_Choice_To_TESSDATA_PREFIX(string? dataDirectory)
    {
        // a system or shared tessdata named by TESSDATA_PREFIX, which Tesseract reads for an empty data path
        var workingDirectory = Directory.GetCurrentDirectory();
        var prefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        var elsewhere = Directory.CreateTempSubdirectory("regira-ocr-");
        Directory.SetCurrentDirectory(elsewhere.FullName);
        Environment.SetEnvironmentVariable("TESSDATA_PREFIX", Path.Combine(AppContext.BaseDirectory, "tessdata"));
        try
        {
            await new OcrManager(new OcrManager.Options { DataDirectory = dataDirectory! }).Test_Read_EN(_assetsDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", prefix);
            Directory.SetCurrentDirectory(workingDirectory);
            elsewhere.Delete();
        }
    }

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
