# Regira.Media.FFMpeg

Video backend for [Regira Media](https://regira.github.io/Regira-Packages/src/Common.Media/), built on [FFMpegCore](https://www.nuget.org/packages/FFMpegCore). `VideoManager` implements `IVideoService` and `ICompressService`: it reads a video's frame rate and size, and compresses a video to VP9/WebM at the frame rate and size it is given. `SnapshotService` extracts one frame as an image, at a position or the first frame.

## Installation

```xml
<PackageReference Include="Regira.Media.FFMpeg" Version="6.*" />
```

- The FFMpeg binaries, `ffmpeg` and `ffprobe`, must be on `PATH`; the package does not ship them. Builds for each platform are listed at [ffmpeg.org](https://ffmpeg.org/download.html).
- `SnapshotService` runs `ffmpeg` through an `IProcessHelper`. The default `ProcessHelper` from Regira.System runs it from a batch file, which is Windows only; on another platform, pass an `IProcessHelper` of your own.

## Documentation

- [Video processing](https://regira.github.io/Regira-Packages/src/Common.Media/docs/video.html) — compression settings, snapshot parameters, and what each one defaults to

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
