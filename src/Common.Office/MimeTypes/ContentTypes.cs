using static Regira.IO.Utilities.ContentTypeUtility;

namespace Regira.Office.MimeTypes;

public static class ContentTypes
{
    public static string CSV { get; set; } = MimeTypesDictionary["csv"].First();
    public static string DOC { get; set; } = MimeTypesDictionary["doc"].First();
    public static string DOCX { get; set; } = MimeTypesDictionary["docx"].First();
    public static string EML { get; set; } = MimeTypesDictionary["eml"].First();
    public static string HTML { get; set; } = MimeTypesDictionary["html"].First();
    public static string MSG { get; set; } = MimeTypesDictionary["msg"].First();
    public static string PDF { get; set; } = MimeTypesDictionary["pdf"].First();
    public static string VCF { get; set; } = MimeTypesDictionary["vcf"].First();
    public static string XLS { get; set; } = MimeTypesDictionary["xls"].First();
    public static string XLSX { get; set; } = MimeTypesDictionary["xlsx"].First();
    public static string XML { get; set; } = MimeTypesDictionary["xml"].First();
}
