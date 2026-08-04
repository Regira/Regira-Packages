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
var vm = new VideoManager();
```

### Get video metadata

```csharp
VideoSettings? info = await vm.GetInfo(videoFile);
// info.Width, info.Height, info.Duration, info.Codec, …
```

### Compress

Encodes to VP9/WebM. Returns `null` if the source is already smaller than the target.

```csharp
IMemoryFile? compressed = await vm.Compress(videoFile, new VideoSettings
{
    // override resolution, bitrate, codec, etc.
});
```

## SnapshotService

Extracts a single frame as an `IImageFile`.

```csharp
var snapshots = new SnapshotService(imageService);

IImageFile? thumb = await snapshots.Snapshot(videoFile,
    size: new Size2D(640, 360),
    time: TimeSpan.FromSeconds(5));
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `input` | `IBinaryFile` | *(required)* | Source video |
| `size` | `Size2D?` | `null` | Output dimensions |
| `time` | `TimeSpan?` | `null` | Frame position (defaults to first frame) |

## Notes

- `SnapshotService` requires an `IImageService` (inject `Regira.Drawing.SkiaSharp.Services.ImageService`).
- Output codec is VP9 / WebM.
- Requires FFMpeg binaries (`ffmpeg`, `ffprobe`) on the host.

## Overview

1. [Index](../README.md) — Overview, models, and API reference
1. [Examples](examples.md) — Thumbnail, watermark, badge builder, and API service pattern
1. **[Video processing](video.md)** — Video compression and snapshot extraction via FFMpeg
