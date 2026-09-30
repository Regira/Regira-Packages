# Regira Media

Regira Media provides video processing — compression and snapshot extraction — via FFMpeg.

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Media.FFMpeg` | `Regira.Media.FFMpeg` | FFMpegCore |

## Installation

```xml
<PackageReference Include="Regira.Media.FFMpeg" Version="6.*" />
```

FFMpeg binaries must be available on `PATH` or configured via `FFMpegCore.GlobalFFOptions`.

## VideoManager

Implements `ICompressService` and `IVideoService`.

```csharp
using Regira.Media.FFMpeg;

var vm = new VideoManager();
```

### Get video metadata

<!-- no-compile -->
```csharp
VideoSettings? info = await vm.GetInfo(videoFile);
// info.Size (Size2D: width × height), info.FrameRate
```

### Compress

Encodes to VP9/WebM. A setting left `null` is derived from the source: `Size` becomes half its width and height,
`FrameRate` 90% of its frame rate.

<!-- no-compile -->
```csharp
IMemoryFile? compressed = await vm.Compress(videoFile, new VideoSettings
{
    Size      = new Size2D(1280, 720),
    FrameRate = 30
});
```

## SnapshotService

Extracts a single frame as an `IImageFile`.

<!-- no-compile -->
```csharp
var snapshots = new SnapshotService(imageService);

IImageFile? thumb = await snapshots.Snapshot(videoFile,
    size: new ImageSize(640, 360),
    time: TimeSpan.FromSeconds(5));
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `input` | `IBinaryFile` | *(required)* | Source video |
| `size` | `ImageSize?` | `null` | Output dimensions |
| `time` | `TimeSpan?` | `null` | Frame position (defaults to first frame) |

## Notes

- `SnapshotService` requires an `IImageService` (inject `Regira.Drawing.SkiaSharp.Services.ImageService`).
- Output codec is VP9 / WebM.
- Requires FFMpeg binaries (`ffmpeg`, `ffprobe`) on the host.

## Overview

1. [Index](../README.md) — Overview, models, and API reference
1. [Examples](examples.md) — Thumbnail, watermark, badge builder, and API service pattern
1. **[Video processing](video.md)** — Video compression and snapshot extraction via FFMpeg
