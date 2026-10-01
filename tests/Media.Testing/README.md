# Media.Testing

NUnit tests for [Media.FFMpeg](../../src/Media.FFMpeg/README.md): the ffmpeg arguments `VideoManager` composes for
`Compress` and the command line `SnapshotService` runs, and both run for real against a generated test video.

## Running

```bash
dotnet test tests/Media.Testing
```

`VideoManagerTests` and `SnapshotServiceTests` need nothing: they read the composed arguments, and a stand-in process
helper writes the frame ffmpeg would. `FFMpegTests` (category `FFMpeg`) runs `ffmpeg` and `ffprobe`, from `PATH` or
from the test output folder, where the Media.FFMpeg project copies an `ffmpeg.exe` and `ffprobe.exe` placed beside
it (both are git-ignored). Without them the fixture is skipped. Its snapshot tests also skip outside Windows, since the
default process helper runs a batch file. To leave the fixture out:

```bash
dotnet test tests/Media.Testing --filter "TestCategory!=FFMpeg"
```
