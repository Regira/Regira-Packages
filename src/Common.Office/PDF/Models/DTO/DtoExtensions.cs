using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Office.PDF.Defaults;
using Regira.Utilities;

namespace Regira.Office.PDF.Models.DTO;

public static class DtoExtensions
{
    /// <summary>The DTO of <paramref name="input"/>, its margins measured at the DTO's 96 DPI.</summary>
    public static HtmlToPdfInputDto ToHtmlToPdfInput(this HtmlInput input)
    {
        var margins = input.Margins.ModifyDpi(input.DPI, DimensionsUtility.DPI.DEFAULT);
        return new()
        {
            HtmlContent = input.HtmlContent ?? string.Empty,
            Orientation = input.Orientation,
            Format = input.Format,
            Margins = [margins.Top, margins.Right, margins.Bottom, margins.Left],
            HeaderHtmlContent = input.HeaderHtmlContent,
            HeaderHeight = input.HeaderHeight ?? 0,
            FooterHtmlContent = input.FooterHtmlContent,
            FooterHeight = input.FooterHeight ?? 0
        };
    }
    public static HtmlInput ToHtmlInput(this HtmlToPdfInputDto input)
        => new()
        {
            HtmlContent = input.HtmlContent,
            Orientation = input.Orientation,
            Format = input.Format,
            Margins = input.Margins,
            HeaderHtmlContent = input.HeaderHtmlContent,
            // the DTO writes a missing height as 0: read it back as missing, so the backend takes its default band
            HeaderHeight = input.HeaderHeight > 0 ? input.HeaderHeight : null,
            FooterHtmlContent = input.FooterHtmlContent,
            FooterHeight = input.FooterHeight > 0 ? input.FooterHeight : null
        };

    public static PdfToImagesOptionsDto ToPdfImagesInput(this PdfToImagesOptions input)
        => new()
        {
            Width = input.Size?.Width,
            Height = input.Size?.Height,
            Format = input.Format
        };
    public static PdfToImagesOptions ToPdfToImagesOptions(this PdfToImagesOptionsDto input)
        => new()
        {
            Size = input is { Width: not null, Height: not null } ? new ImageSize(input.Width.Value, input.Height.Value) : PdfDefaults.ImageSize,
            Format = input.Format ?? ImageFormat.Jpeg
        };
}