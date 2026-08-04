using Regira.Dimensions;
using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Models.DTO.Extensions;
using Regira.Media.Drawing.Services;
using Regira.Office.Barcodes.Defaults;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Drawing;

public static class BarcodeImageModelExtensions
{
    public static IImageLayer ToImageLayer(this BarcodeImageLayerDto input, ImageSize targetSize, int? dpi)
    {
        return new ImageLayer<BarcodeInput>
        {
            Source = input.ToBarcodeInput(targetSize, dpi),
            Options = input.DrawOptions?.ToImageLayerOptions(targetSize, dpi)
        };
    }

    public static BarcodeInput ToBarcodeInput(this BarcodeImageLayerDto dto, ImageSize targetSize, int? dpi = null)
    {
        dpi ??= ImageLayerDefaults.Dpi;
        var unit = dto.DrawOptions?.DimensionUnit ?? ImageLayerDefaults.DimensionUnit;
        var size = new Size2D(dto.DrawOptions?.Width ?? BarcodeDefaults.Size.Width, dto.DrawOptions?.Height ?? BarcodeDefaults.Size.Height);

        return new BarcodeInput
        {
            Format = dto.Format ?? BarcodeDefaults.Format,
            Content = dto.Content,
            Size = size.ToImageSize(unit, targetSize, dpi.Value)!.Value,
            Color = dto.Color ?? BarcodeDefaults.Color,
            BackgroundColor = dto.BackgroundColor ?? BarcodeDefaults.BackgroundColor
        };
    }

    public static QRCodeInput ToQRCodeInput(this BarcodeImageLayerDto dto, ImageSize targetSize, int? dpi = null)
    {
        dpi ??= ImageLayerDefaults.Dpi;
        var unit = dto.DrawOptions?.DimensionUnit ?? ImageLayerDefaults.DimensionUnit;
        var size = new Size2D(dto.DrawOptions?.Width ?? BarcodeDefaults.Size.Width, dto.DrawOptions?.Height ?? BarcodeDefaults.Size.Width);

        return new QRCodeInput
        {
            Format = BarcodeFormat.QRCode,
            Content = dto.Content,
            Size = size.ToImageSize(unit, targetSize, dpi.Value)!.Value,
            Color = dto.Color ?? BarcodeDefaults.Color,
            BackgroundColor = dto.BackgroundColor ?? BarcodeDefaults.BackgroundColor
        };
    }
}