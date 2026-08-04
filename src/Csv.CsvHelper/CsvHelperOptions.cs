using Regira.Office.Csv.Models;

namespace Regira.Office.Csv.CsvHelper;

public class CsvHelperOptions : CsvOptions
{
    public bool IgnoreBadData = false;
    public bool PreserveWhitespace = false;
}
