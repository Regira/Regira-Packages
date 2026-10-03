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

The FFMpeg binaries, `ffmpeg` and `ffprobe`, must be on `PATH`. `VideoManager` also finds them where
`FFMpegCore.GlobalFFOptions` points, but `SnapshotService` runs `ffmpeg` by name.

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

Encodes to VP9/WebM at a fixed quality (constant rate factor 31). `FrameRate` sets the output's frames per second
and `Size` its dimensions. Left `null`, `FrameRate` keeps the source's frame rate and `Size` becomes half its width and
height; the settings object itself is not changed.

<!-- no-compile -->
```csharp
IMemoryFile? compressed = await vm.Compress(videoFile, new VideoSettings
{
    Size      = new Size2D(1280, 720),
    FrameRate = 30
});
```

## SnapshotService

Extracts a single frame as an `IImageFile`. It runs `ffmpeg` through an `IProcessHelper`: the default
`ProcessHelper` from `Regira.System` runs it from a batch file, which is Windows only, so on another platform pass an
`IProcessHelper` of your own.

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
| `size` | `ImageSize?` | `null` | Output dimensions (`null`: the video's own, read with `ffprobe`) |
| `time` | `TimeSpan?` | `null` | Frame position (`null`: the first frame) |

## Notes

- `SnapshotService` requires an `IImageService` (inject `Regira.Drawing.SkiaSharp.Services.ImageService`).
- Output codec is VP9 / WebM.

## Overview

1. [Index](../README.md) — Overview, models, and API reference
1. [Examples](examples.md) — Thumbnail, watermark, badge builder, and API service pattern
1. **[Video processing](video.md)** — Video compression and snapshot extraction via FFMpeg
