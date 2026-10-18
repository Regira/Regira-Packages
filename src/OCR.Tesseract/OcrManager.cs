using Regira.Globalization.Utilities;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.OCR.Abstractions;
using Regira.Office.OCR.Models.DTO;
using Tesseract;

namespace Regira.Office.OCR.Tesseract;

public class OcrManager : IOcrService
{
    public class Options
    {
        /// <summary>
        /// 2-letter code ISO language
        /// </summary>
        public string Language { get; set; } = "en";
        /// <summary>
        /// Put language models in this directory
        /// Download more models at https://github.com/tesseract-ocr/tessdata/
        /// A relative path that does not exist under the working directory resolves against the app's output folder.
        /// An empty one is passed to Tesseract, which then reads <c>TESSDATA_PREFIX</c>.
        /// </summary>
        public string DataDirectory { get; set; } = "./tessdata";
    }

    private readonly string _lang;
    private readonly string _dataDirectory;
    public OcrManager(Options? options = null)
    {
        options ??= new Options();
        _lang = options.Language;
        // the build copies tessdata/ to the output folder, which is not the working directory under dotnet run
        // an empty one goes to Tesseract as it is, which then reads TESSDATA_PREFIX
        _dataDirectory = string.IsNullOrEmpty(options.DataDirectory) || Path.IsPathRooted(options.DataDirectory) || Directory.Exists(options.DataDirectory)
            ? options.DataDirectory
            : Path.Combine(AppContext.BaseDirectory, options.DataDirectory);
    }


    public Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken token = default)
    {
        var ocrLang = ConvertLang(lang ?? _lang);
        using var engine = new TesseractEngine(_dataDirectory, ocrLang, EngineMode.Default);
        using var img = Pix.LoadFromMemory(imgFile.GetBytes());
        var result = engine.Process(img);
        var text = result.GetText();

        return Task.FromResult(new OcrResult
        {
            Text = text,
            Language = ocrLang
        });
    }

    public string ConvertLang(string? lang)
        => (!string.IsNullOrEmpty(lang) ? LanguageUtility.ToIso3Code(lang) : null)
            ?? "eng";
}