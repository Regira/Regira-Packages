using Regira.Office.Models;

namespace Regira.Office.Word.Models;

public record ConversionOptions
{
    public FileFormat OutputFormat { get; set; } = FileFormat.Docx;
    public DocumentSettings? Settings { get; set; }
    public bool AutoScaleTables { get; set; } = true;
    public bool AutoScalePictures { get; set; } = true;
}