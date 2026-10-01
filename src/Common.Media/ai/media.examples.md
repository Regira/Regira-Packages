# Media (Drawing) — Example: Product Image Processing

> Context: An e-commerce API generates thumbnails from uploaded product photos, adds a watermark, converts images to WebP for serving, and takes a preview frame from each product video.

## DI Registration

<!-- no-compile -->
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

<!-- no-compile -->
```csharp
public async Task<byte[]> ProcessProductImage(byte[] uploadedBytes)
{
    using var original = (await _imageService.Parse(uploadedBytes))!;
    using var resized  = await _imageService.Resize(original, new ImageSize(800, 800));
    using var webp     = await _imageService.ChangeFormat(resized, ImageFormat.Webp);
    return webp.GetBytes()!;
}
```

## Generate a thumbnail

<!-- no-compile -->
```csharp
public async Task<byte[]> CreateThumbnail(byte[] imageBytes)
{
    using var img       = (await _imageService.Parse(imageBytes))!;
    using var thumbnail = await _imageService.Resize(img, new ImageSize(120, 120));
    return thumbnail.GetBytes()!;
}
```

## Add a "SALE" watermark

<!-- no-compile -->
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

    return result.GetBytes()!;
}
```

## Preview frame from a product video

`SnapshotService` (`Regira.Media.FFMpeg`) needs `ffmpeg` on `PATH`, and its default process helper is Windows only.

```csharp
using Regira.IO.Abstractions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.FFMpeg;

public class ProductVideoPreviews(SnapshotService snapshots)
{
    // the frame two seconds in, sized for the product page's video tile
    public Task<IImageFile?> CreatePreview(IBinaryFile video)
        => snapshots.Snapshot(video, new ImageSize(320, 180), TimeSpan.FromSeconds(2));
}
```
