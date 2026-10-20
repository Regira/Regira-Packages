using Office.PDF.Testing.Abstractions;

namespace Office.PDF.Testing;

/// <summary>
/// Empties <c>Assets/Output</c> once per run, before any fixture writes to it, so every file there comes from this
/// run and a renamed test leaves nothing behind.
/// </summary>
[SetUpFixture]
public class OutputSetUp
{
    [OneTimeSetUp]
    public void EmptyOutput()
    {
        if (!Directory.Exists(PdfAssetsTestsBase.OutputDirectory))
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(PdfAssetsTestsBase.OutputDirectory, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a file another run or a viewer holds open stays; this run writes it again
            }
        }
    }
}
