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
        /// </summary>
        public string DataDirectory { get; set; } = "./tessdata";
    }

    private readonly string _lang;
    private readonly string _dataDirectory;
    public OcrManager(Options? options = null)
    {
        options ??= new Options();
        _lang = options.Language;
        _dataDirectory = options.DataDirectory;
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