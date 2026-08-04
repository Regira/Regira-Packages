using System.Globalization;

namespace Regira.Office.Csv.Models;

public class CsvOptions
{
    public static string DEFAULT_CULTURE { get; set; } = "en-US";

    public string Delimiter { get; set; } = ",";
    public CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo(DEFAULT_CULTURE);
}
