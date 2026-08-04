using Regira.Dimensions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services;
using Regira.Media.Drawing.Utilities;
using Regira.Utilities;

namespace Regira.Media.Drawing.Models.DTO.Extensions;

public static class DtoExtensions
{
    public static ImageLayerOptions ToImageLayerOptions(this ImageLayerOptionsDto dto, ImageSize targetSize, int? dpi = null)
    {
        dpi ??= ImageLayerDefaults.Dpi;
        var unit = dto.DimensionUnit ?? ImageLayerDefaults.DimensionUnit;

        return new ImageLayerOptions
        {
            Size = new Size2D(dto.Width ?? 0, dto.Height ?? 0).ToImageSize(unit, targetSize, dpi.Value),
            Margin = PixelParserUtility.ToPixels(dto.Margin ?? ImageLayerDefaults.Margin, unit, Math.Max(targetSize.Width, targetSize.Height), dpi.Value),
            Position = dto.Position ?? ImagePosition.Absolute,
            Offset = new Position2D(dto.Top, dto.Left, dto.Bottom, dto.Right).ToImageEdgeOffset(unit, targetSize, dpi.Value),
            Rotation = dto.Rotation ?? ImageLayerDefaults.Rotation,
            Opacity = dto.Opacity ?? ImageLayerDefaults.Opacity
        };
    }
    public static ImageLayer ToImageLayer(this ImageLayerDto dto, ImageSize targetSize, int? dpi)
    {
        return new ImageLayer
        {
            Source = dto.Bytes.ToBinaryFile().ToImageFile(),
            Options = dto.DrawOptions?.ToImageLayerOptions(targetSize, dpi),
        };
    }

    public static LabelImageOptions ToLabelImageOptions(this LabelImageLayerDto dto, ImageSize targetSize, int? dpi)
    {
        dpi ??= ImageLayerDefaults.Dpi;
        var unit = dto.LabelOptions?.DimensionUnit ?? ImageLayerDefaults.DimensionUnit;

        return new LabelImageOptions
        {
            Text = dto.Text,
            FontSize = PixelParserUtility.ToPixels(dto.LabelOptions?.FontSize ?? LabelImageDefaults.FontSize, unit, Math.Max(targetSize.Width, targetSize.Height), dpi.Value),
            Padding = PixelParserUtility.ToPixels(dto.LabelOptions?.Padding ?? LabelImageDefaults.Padding, unit, Math.Max(targetSize.Width, targetSize.Height), dpi.Value),
            FontName = dto.LabelOptions?.FontName ?? LabelImageDefaults.FontName,
            TextColor = dto.LabelOptions?.TextColor ?? LabelImageDefaults.TextColor,
            BackgroundColor = dto.LabelOptions?.BackgroundColor ?? LabelImageDefaults.BackgroundColor,
        };
    }
    public static IImageLayer ToImageLayer(this LabelImageLayerDto dto, ImageSize targetSize, int? dpi)
    {
        return new ImageLayer<LabelImageOptions>
        {
            Source = dto.ToLabelImageOptions(targetSize, dpi),
            Options = dto.DrawOptions?.ToImageLayerOptions(targetSize, dpi)
        };
    }

    public static CanvasImageOptions ToCanvasImageOptions(this CanvasImageDto dto, ImageSize targetSize)
    {
        var dpi = dto.Dpi ?? ImageLayerDefaults.Dpi;
        var dimUnit = dto.DimensionUnit ?? ImageLayerDefaults.DimensionUnit;

        return new CanvasImageOptions
        {
            Size = new Size2D(dto.Width, dto.Height).ToImageSize(dimUnit, targetSize, dpi)!.Value,
            BackgroundColor = dto.BackgroundColor ?? ImageDefaults.BackgroundColor,
            ImageFormat = dto.ImageFormat ?? ImageDefaults.Format
        };
    }
    public static IImageLayer ToImageLayer(this CanvasImageLayerDto dto, ImageSize targetSize)
    {
        var dpi = dto.Dpi ?? ImageLayerDefaults.Dpi;
        var dimUnit = dto.DimensionUnit ?? ImageLayerDefaults.DimensionUnit;

        return new ImageLayer<CanvasImageOptions>
        {
            Source = dto.ToCanvasImageOptions(targetSize),
            Options = new ImageLayerOptions
            {
                Margin = DimensionsUtility.GetPixels(dto.DrawOptions?.Margin ?? 0, dimUnit, Math.Max(targetSize.Width, targetSize.Height), dpi),
                Position = dto.DrawOptions?.Position ?? ImagePosition.Absolute,
                Offset = new Position2D(dto.DrawOptions?.Top, dto.DrawOptions?.Left, dto.DrawOptions?.Bottom, dto.DrawOptions?.Right).ToImageEdgeOffset(dimUnit, targetSize, dpi),
                Rotation = dto.DrawOptions?.Rotation ?? ImageLayerDefaults.Rotation,
                Opacity = dto.DrawOptions?.Opacity ?? ImageLayerDefaults.Opacity
            }
        };
    }
}