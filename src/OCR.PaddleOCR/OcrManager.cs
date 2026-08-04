using OpenCvSharp;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.OCR.Abstractions;
using Regira.Office.OCR.Models.DTO;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;

namespace Regira.Office.OCR.PaddleOCR;

public class OcrManager : IOcrService
{
    // Paddle Inference 3.x crashes (access violation in PD_PredictorCreate2) when predictors are created
    // concurrently, so serialize construction. Running the predictors themselves stays parallel.
    private static readonly object CreateLock = new();

    public Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken token = default)
    {
        FullOcrModel model = ConvertLang(lang);
        PaddleOcrAll all;
        lock (CreateLock)
        {
            all = new PaddleOcrAll(model, PaddleDevice.OneDnn())
            {
                AllowRotateDetection = false,//true,
                Enable180Classification = false,
            };
        }
        using var _ = all;
        var bytes = imgFile.GetBytes();
        using Mat src = Cv2.ImDecode(bytes!, ImreadModes.Color);
        PaddleOcrResult result = all.Run(src);
        var content = result.Text;
        return Task.FromResult(new OcrResult
        {
            Text = content,
            Language = model.DetectionModel.ToString() ?? "EnglishV5"
        });
    }


    /// <summary>
    /// Non-English languages written in Latin script, recognized by <see cref="LocalFullModels.LatinV5"/>.
    /// </summary>
    private static readonly HashSet<string> LatinLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "af", "az", "bs", "ca", "cs", "cy", "da", "de", "es", "et", "fr", "ga", "hr", "hu", "id", "is",
        "it", "ku", "la", "lt", "lv", "mi", "ms", "mt", "nl", "no", "oc", "pl", "pt", "ro", "sk", "sl",
        "sq", "sv", "sw", "tl", "tr", "uz", "vi"
    };

    /// <summary>
    /// Maps an ISO 639-1 language code to the PP-OCRv5 model that recognizes its script.
    /// Unknown or missing codes fall back to English.
    /// </summary>
    public FullOcrModel ConvertLang(string? lang = null)
    {
        var code = lang?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return LocalFullModels.EnglishV5;
        }
        if (LatinLanguages.Contains(code))
        {
            return LocalFullModels.LatinV5;
        }
        return code.ToLowerInvariant() switch
        {
            "zh" or "cn" => LocalFullModels.ChineseV5,
            "ko" => LocalFullModels.KoreanV5,
            "ar" or "fa" or "ug" or "ur" => LocalFullModels.ArabicV5,
            "ru" or "be" or "uk" => LocalFullModels.EastSlavicV5,
            "bg" or "mk" or "mn" or "sr" => LocalFullModels.CyrillicV5,
            "hi" or "mr" or "ne" or "sa" => LocalFullModels.DevanagariV5,
            "el" => LocalFullModels.GreekV5,
            "ta" => LocalFullModels.TamilV5,
            "te" => LocalFullModels.TeluguV5,
            "th" => LocalFullModels.ThaiV5,
            _ => LocalFullModels.EnglishV5,
        };
    }
}
