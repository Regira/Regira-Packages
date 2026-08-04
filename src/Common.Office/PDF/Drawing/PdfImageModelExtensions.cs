using Regira.Dimensions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Models.DTO.Extensions;
using Regira.Media.Drawing.Services;
using Regira.Office.PDF.Defaults;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Drawing;

public static class PdfImageModelExtensions
{
    public static IImageLayer ToImageLayer(this PdfImageLayerDto input, ImageSize targetSize, int? dpi)
    {
        return new ImageLayer<PdfToImageLayerOptions>
        {
            Source = input.ToPdfToImageLayerOptions(targetSize, dpi),
            Options = input.DrawOptions?.ToImageLayerOptions(targetSize, dpi)
        };
    }

    public static PdfToImageLayerOptions ToPdfToImageLayerOptions(this PdfImageLayerDto dto, ImageSize targetSize, int? dpi = null)
    {
        dpi ??= ImageLayerDefaults.Dpi;

        return new PdfToImageLayerOptions
        {
            File = dto.Bytes.ToMemoryFile(),
            Page = dto.Page ?? 1,
            Size = new Size2D(dto.DrawOptions?.Width ?? PdfDefaults.ImageSize.Width, dto.DrawOptions?.Height ?? PdfDefaults.ImageSize.Height)
                .ToImageSize(dto.DrawOptions?.DimensionUnit ?? ImageLayerDefaults.DimensionUnit, targetSize, dpi.Value),
            Format = dto.Format ?? PdfDefaults.ImageFormat
        };
    }

    public static PdfToImagesOptions ToPdfToImageOptions(this PdfToImageLayerOptions options)
    {
        return new PdfToImagesOptions
        {
            Format = options.Format,
            Size = options.Size
        };
    }
}