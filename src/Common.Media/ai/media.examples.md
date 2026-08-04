# Media (Drawing) — Example: Product Image Processing

> Context: An e-commerce API generates thumbnails from uploaded product photos, adds a watermark, and converts images to WebP for serving.

## DI Registration

```csharp
// Program.cs
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();
services.AddSingleton<IImageCreator, CanvasImageCreator>();
services.AddSingleton<IImageCreator, LabelImageCreator>();
services.AddSingleton<IImageCreator>(sp =>
    AggregateImageCreator.Create(
        sp.GetRequiredService<IImageService>(),
        sp.GetServices<IImageCreator>()
    ));
```

## Resize and convert uploaded image

```csharp
public async Task<byte[]> ProcessProductImage(byte[] uploadedBytes)
{
    using var original = (await _imageService.Parse(uploadedBytes))!;
    using var resized  = await _imageService.Resize(original, new ImageSize(800, 800));
    using var webp     = await _imageService.ChangeFormat(resized, ImageFormat.Webp);
    return webp.Bytes!;
}
```

## Generate a thumbnail

```csharp
public async Task<byte[]> CreateThumbnail(byte[] imageBytes)
{
    using var img       = (await _imageService.Parse(imageBytes))!;
    using var thumbnail = await _imageService.Resize(img, new ImageSize(120, 120));
    return thumbnail.Bytes!;
}
```

## Add a "SALE" watermark

```csharp
public async Task<byte[]> AddWatermark(byte[] imageBytes)
{
    using var photo = (await _imageService.Parse(imageBytes))!;

    using var result = await new ImageBuilder(_imageService, _imageCreators)
        .SetBaseLayer(photo)
        .Add(new ImageLayer<LabelImageOptions>
        {
            Source  = new() { Text = "SALE", FontSize = 28, TextColor = "#FF0000",
                              BackgroundColor = Color.Transparent },
            Options = new() { Position = ImagePosition.Right | ImagePosition.Bottom,
                              Margin = 12, Rotation = -20, Opacity = 0.6f }
        })
        .Build();

    return result.Bytes!;
}
```
