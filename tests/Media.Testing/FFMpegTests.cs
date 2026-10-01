using System.Diagnostics;
using Regira.Dimensions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.FFMpeg;
using Regira.Media.Video.Models;
using ImageService = Regira.Drawing.SkiaSharp.Services.ImageService;

namespace Media.Testing;

// Runs ffmpeg and ffprobe for real, which SnapshotService finds on PATH. The output folder goes on PATH too, since
// the Media.FFMpeg project copies an ffmpeg.exe and ffprobe.exe placed beside it there; without either, every test
// here is skipped.
[TestFixture]
[Category("FFMpeg")]
[NonParallelizable]
public class FFMpegTests
{
    private string _videoPath = null!;

    [OneTimeSetUp]
    public void CreateSourceVideo()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Split(Path.PathSeparator).Contains(AppContext.BaseDirectory))
        {
            Environment.SetEnvironmentVariable("PATH", $"{AppContext.BaseDirectory}{Path.PathSeparator}{path}");
        }

        _videoPath = Path.Combine(Path.GetTempPath(), $"regira-ffmpeg-{Guid.NewGuid():N}.mp4");
        // a 3-second 320x240 test pattern at 30 fps
        var exitCode = Run("ffmpeg", $"-hide_banner -loglevel error -f lavfi -i testsrc=duration=3:size=320x240:rate=30 -pix_fmt yuv420p \"{_videoPath}\"");
        if (exitCode == null)
        {
            Assert.Ignore("ffmpeg is not on PATH or in the test output folder");
        }
        Assert.That(exitCode, Is.EqualTo(0), "ffmpeg could not create the source video");
    }

    [OneTimeTearDown]
    public void DeleteSourceVideo()
    {
        if (File.Exists(_videoPath))
        {
            File.Delete(_videoPath);
        }
    }

    private static int? Run(string executable, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true })!;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private BinaryFileItem SourceVideo() => new() { Path = _videoPath };

    [Test]
    public async Task Compress_Writes_The_FrameRate_And_Size_It_Is_Given()
    {
        var videoManager = new VideoManager();

        using var compressed = (await videoManager.Compress(SourceVideo(), new VideoSettings { FrameRate = 10, Size = new Size2D(160, 120) }))!;
        var info = await videoManager.GetInfo(new BinaryFileItem { Bytes = compressed.GetBytes() });

        Assert.That(info!.FrameRate, Is.EqualTo(10));
        Assert.That(info.Size, Is.EqualTo(new Size2D(160, 120)));
    }

    [Test]
    public async Task Compress_Keeps_The_Source_FrameRate_And_Halves_The_Size_By_Default()
    {
        var videoManager = new VideoManager();
        var settings = new VideoSettings();

        using var compressed = (await videoManager.Compress(SourceVideo(), settings))!;
        var info = await videoManager.GetInfo(new BinaryFileItem { Bytes = compressed.GetBytes() });

        Assert.That(info!.FrameRate, Is.EqualTo(30));
        Assert.That(info.Size, Is.EqualTo(new Size2D(160, 120)));
        Assert.That(settings.FrameRate, Is.Null, "the caller's settings are left as they were");
    }

    [TestCase(null)]
    [TestCase(1.0)]
    [TestCase(2.5)]
    public async Task Snapshot_Returns_A_Frame(double? seconds)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("ProcessHelper.ExecuteCommand runs a Windows batch file");
        }
        var service = new SnapshotService(new ImageService());

        using var thumb = await service.Snapshot(SourceVideo(), new ImageSize(160, 120), seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : null);

        Assert.That(thumb, Is.Not.Null);
        Assert.That(thumb!.Size, Is.EqualTo(new ImageSize(160, 120)));
    }
}
