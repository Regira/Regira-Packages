namespace Regira.Office.Barcodes.Models.DTO;

public static class DtoExtensions
{
    public static BarcodeInputDto ToBarcodeInputDto(this BarcodeInput input)
        => new()
        {
            Content = input.Content,
            Format = input.Format,
            Width = input.Size.Width > 0 ? input.Size.Width : null,
            Height = input.Size.Height > 0 ? input.Size.Height : null,
            Color = input.Color.Hex,
            BackgroundColor = input.BackgroundColor.Hex
        };
    public static BarcodeInput ToBarcodeInput(this BarcodeInputDto dto)
        => new()
        {
            Content = dto.Content,
            Format = dto.Format ?? BarcodeFormat.QRCode,
            Size = new[] { dto.Width ?? 400, dto.Height ?? 100 },
            Color = dto.Color ?? "#000000",
            BackgroundColor = dto.BackgroundColor ?? "#FFFFFF"
        };

    public static QRCodeInputDto ToQRCodeInputDto(this QRCodeInput input)
        => new()
        {
            Content = input.Content,
            Size = input.Size.Width > 0 ? input.Size.Width : 200,
            Color = input.Color.Hex
        };
    public static QRCodeInput ToQRCodeInput(this QRCodeInputDto dto)
        => new()
        {
            Content = dto.Content,
            Size = new[] { dto.Size > 0 ? dto.Size : 200, dto.Size > 0 ? dto.Size : 200 },
            Color = dto.Color
        };
}