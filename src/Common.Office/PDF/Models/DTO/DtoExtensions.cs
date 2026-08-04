using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Office.PDF.Defaults;

namespace Regira.Office.PDF.Models.DTO;

public static class DtoExtensions
{
    public static HtmlToPdfInputDto ToHtmlToPdfInput(this HtmlInput input)
        => new()
        {
            HtmlContent = input.HtmlContent ?? string.Empty,
            Orientation = input.Orientation,
            Format = input.Format,
            Margins = [input.Margins.Top, input.Margins.Right, input.Margins.Bottom, input.Margins.Left],
            HeaderHtmlContent = input.HeaderHtmlContent,
            HeaderHeight = input.HeaderHeight ?? 0,
            FooterHtmlContent = input.FooterHtmlContent,
            FooterHeight = input.FooterHeight ?? 0
        };
    public static HtmlInput ToHtmlInput(this HtmlToPdfInputDto input)
        => new()
        {
            HtmlContent = input.HtmlContent,
            Orientation = input.Orientation,
            Format = input.Format,
            Margins = input.Margins,
            HeaderHtmlContent = input.HeaderHtmlContent,
            HeaderHeight = input.HeaderHeight,
            FooterHtmlContent = input.FooterHtmlContent,
            FooterHeight = input.FooterHeight
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