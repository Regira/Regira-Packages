using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;

namespace Regira.Office.Word.Models.DTO;

public static class DtoExtensions
{
    public static WordDocumentInputDto ToWordDocumentInputDto(this WordTemplateInput input) => new()
    {
        TemplateBytes = input.Template.GetBytes() ?? throw new ArgumentException("Template has no content.", nameof(input)),
        GlobalParameters = input.GlobalParameters,
        CollectionParameters = input.CollectionParameters,
        Images = input.Images?.Select(ToWordImageModel).ToList(),
        DocumentParameters = input.DocumentParameters?.ToDictionary(dp => dp.Key, dp => dp.Value.ToWordDocumentInputDto()),
        Headers = input.Headers?.Select(h => new WordHeaderFooterModel { Template = ToWordDocumentInputDto(h.Template), Type = h.Type }).ToList(),
        Footers = input.Footers?.Select(f => new WordHeaderFooterModel { Template = ToWordDocumentInputDto(f.Template), Type = f.Type }).ToList(),
        Options = input.Options
    };
    public static WordTemplateInput ToWordTemplateInput(this WordDocumentInputDto dto) => new()
    {
        Template = dto.TemplateBytes.ToMemoryFile(),
        GlobalParameters = dto.GlobalParameters,
        CollectionParameters = dto.CollectionParameters,
        Images = dto.Images?.Select(ToWordImage).ToList(),
        DocumentParameters = dto.DocumentParameters?.ToDictionary(dp => dp.Key, dp => ToWordTemplateInput(dp.Value)),
        Headers = dto.Headers?.Select(h => new WordHeaderFooterInput { Template = ToWordTemplateInput(h.Template), Type = h.Type }).ToList(),
        Footers = dto.Footers?.Select(f => new WordHeaderFooterInput { Template = ToWordTemplateInput(f.Template), Type = f.Type }).ToList(),
        Options = dto.Options ?? new()
    };

    private static WordImageModel ToWordImageModel(WordImage image) => new()
    {
        Name = image.Name,
        Bytes = image.File?.GetBytes() ?? [],
        Width = image.Size?.Width,
        Height = image.Size?.Height,
        HorizontalAlignment = image.HorizontalAlignment
    };
    private static WordImage ToWordImage(WordImageModel model) => new()
    {
        Name = model.Name!,
        File = model.Bytes.ToMemoryFile(),
        // typed: a bare null would convert through ImageSize's implicit operator from int[], which throws on null
        Size = model is { Width: { } width, Height: { } height } ? new ImageSize(width, height) : (ImageSize?)null,
        HorizontalAlignment = model.HorizontalAlignment
    };
}