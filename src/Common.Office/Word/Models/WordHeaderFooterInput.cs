namespace Regira.Office.Word.Models;

public record WordHeaderFooterInput
{
    public WordTemplateInput Template { get; set; } = null!;
    public HeaderFooterType Type { get; set; } = HeaderFooterType.Default;
}