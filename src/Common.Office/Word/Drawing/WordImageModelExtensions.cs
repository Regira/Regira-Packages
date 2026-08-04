using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Models.DTO.Extensions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Drawing;

public static class WordImageModelExtensions
{
    public static IImageLayer ToImageLayer(this WordToImageLayerDto input, ImageSize targetSize, int? dpi)
    {
        return new ImageLayer<WordToImageLayerOptions>
        {
            Source = input.ToWordToImageLayerOptions(),
            Options = input.DrawOptions?.ToImageLayerOptions(targetSize, dpi)
        };
    }

    public static WordToImageLayerOptions ToWordToImageLayerOptions(this WordToImageLayerDto dto)
    {
        return new WordToImageLayerOptions
        {
            File = dto.Bytes.ToMemoryFile(),
            Page = dto.Page ?? 1
        };
    }

    public static WordTemplateInput ToWordTemplateInput(this WordToImageLayerOptions options)
    {
        return new WordTemplateInput
        {
            Template = options.File
        };
    }
}