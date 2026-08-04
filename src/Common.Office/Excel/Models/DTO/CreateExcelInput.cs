namespace Regira.Office.Excel.Models.DTO;

public class ExcelSheetInputDto
{
    public string? Name { get; set; }
    public IList<IDictionary<string, object?>> Data { get; set; } = null!;
}