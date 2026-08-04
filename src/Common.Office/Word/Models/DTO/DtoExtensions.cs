using Regira.IO.Extensions;

namespace Regira.Office.Word.Models.DTO;

public static class DtoExtensions
{
    public static WordDocumentInputDto ToWordDocumentInputDto(this WordTemplateInput input) => new()
    {
        TemplateBytes = input.Template.GetBytes() ?? throw new ArgumentException("Template has no content.", nameof(input)),
        GlobalParameters = input.GlobalParameters,
        CollectionParameters = input.CollectionParameters,
        Images = input.Images?.Select(img => new WordImageModel { Name = img.Name, Bytes = img.File?.GetBytes() ?? [] }).ToList(),
        DocumentParameters = input.DocumentParameters?.ToDictionary(dp => dp.Key, dp => dp.Value.ToWordDocumentInputDto()),
        Headers = input.Headers?.Select(h => new WordHeaderFooterModel { Template = ToWordDocumentInputDto(h.Template), Type = h.Type }).ToList(),
        Footers = input.Footers?.Select(f => new WordHeaderFooterModel { Template = ToWordDocumentInputDto(f.Template), Type = f.Type }).ToList()
    };
    public static WordTemplateInput ToWordTemplateInput(this WordDocumentInputDto dto) => new()
    {
        Template = dto.TemplateBytes.ToMemoryFile(),
        GlobalParameters = dto.GlobalParameters,
        CollectionParameters = dto.CollectionParameters,
        Images = dto.Images?.Select(img => new WordImage { Name = img.Name!, File = img.Bytes.ToMemoryFile() }).ToList(),
        DocumentParameters = dto.DocumentParameters?.ToDictionary(dp => dp.Key, dp => ToWordTemplateInput(dp.Value)),
        Headers = dto.Headers?.Select(h => new WordHeaderFooterInput { Template = ToWordTemplateInput(h.Template), Type = h.Type }).ToList(),
        Footers = dto.Footers?.Select(f => new WordHeaderFooterInput { Template = ToWordTemplateInput(f.Template), Type = f.Type }).ToList()
    };
}