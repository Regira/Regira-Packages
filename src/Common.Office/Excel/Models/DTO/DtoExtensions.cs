using Regira.Utilities;

namespace Regira.Office.Excel.Models.DTO;

public static class DtoExtensions
{
    public static ExcelSheetInputDto ToExcelSheetInputDto(this ExcelSheet input)
        => new()
        {
            Name = input.Name,
            Data = input.Data?.Select(item => item as IDictionary<string, object?> ?? DictionaryUtility.ToDictionary(item)).ToList() ?? []
        };
    public static ExcelSheet ToExcelSheet(this ExcelSheetInputDto dto)
        => new()
        {
            Name = dto.Name,
            Data = dto.Data.Cast<object>().ToList() ?? []
        };
}