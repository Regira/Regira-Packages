using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.FFMpeg;
using Regira.System;
using Regira.System.Abstractions;
using ImageService = Regira.Drawing.SkiaSharp.Services.ImageService;

namespace Media.Testing;

// The ffmpeg command line SnapshotService composes, and the temporary files it leaves. A stand-in process helper
// writes the frame ffmpeg would, so no ffmpeg is needed.
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SnapshotServiceTests
{
    private static readonly ImageSize Size = new(64, 48);

    private static async Task<FrameWriter> Snapshot(TimeSpan? time, BinaryFileItem? input = null)
    {
        var processHelper = new FrameWriter();
        var service = new SnapshotService(new ImageService(), processHelper);

        using var thumb = await service.Snapshot(input ?? new BinaryFileItem { Bytes = [0] }, Size, time);

        Assert.That(thumb, Is.Not.Null);
        return processHelper;
    }

    [Test]
    public async Task Without_A_Time_Takes_The_First_Frame()
    {
        var command = (await Snapshot(null)).Command!;

        Assert.That(command, Does.StartWith("ffmpeg -i "));
        Assert.That(command, Does.Not.Contain("-ss"));
    }

    // before -i, so ffmpeg seeks the input instead of decoding everything up to the position
    [TestCase(5, "ffmpeg -ss 5 -i ")]
    [TestCase(1.5, "ffmpeg -ss 1.5 -i ")]
    [TestCase(3723.25, "ffmpeg -ss 3723.25 -i ")]
    public async Task Seeks_To_The_Time_In_Seconds_Before_Reading_The_Input(double seconds, string expected)
    {
        var command = (await Snapshot(TimeSpan.FromSeconds(seconds))).Command!;

        Assert.That(command, Does.StartWith(expected));
    }

    [Test]
    public async Task Leaves_No_Temporary_Files()
    {
        var run = await Snapshot(TimeSpan.FromSeconds(1));

        Assert.That(File.Exists(run.FramePath), Is.False, "the frame ffmpeg wrote");
        Assert.That(File.Exists(Path.ChangeExtension(run.FramePath, null)), Is.False, "a file beside the frame");
        Assert.That(File.Exists(run.InputPath), Is.False, "the input written to disk for ffmpeg");
    }

    [Test]
    public async Task Leaves_No_Temporary_Files_When_FFMpeg_Fails()
    {
        var processHelper = new FrameWriter { ExitCode = 1 };
        var service = new SnapshotService(new ImageService(), processHelper);

        await Assert.ThrowsAsync<Exception>(() => service.Snapshot(new BinaryFileItem { Bytes = [0] }, Size, TimeSpan.FromSeconds(1)));

        Assert.That(File.Exists(processHelper.FramePath), Is.False, "the frame ffmpeg wrote");
        Assert.That(File.Exists(processHelper.InputPath), Is.False, "the input written to disk for ffmpeg");
    }

    [Test]
    public async Task Keeps_An_Input_File_It_Did_Not_Write()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"regira-snapshot-input-{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(inputPath, [0]);
        try
        {
            var run = await Snapshot(null, new BinaryFileItem { Path = inputPath });

            Assert.That(run.InputPath, Is.EqualTo(inputPath));
            Assert.That(File.Exists(inputPath), Is.True);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    private sealed class FrameWriter : IProcessHelper
    {
        public int ExitCode { get; init; }
        public string? Command { get; private set; }
        public string? InputPath { get; private set; }
        public string? FramePath { get; private set; }

        public IProcessOutput ExecuteCommand(string command, bool waitForOutput = false)
        {
            Command = command;
            // the input is the command's first quoted argument, the output its last
            var quoted = command.Split('"');
            InputPath = quoted[1];
            FramePath = quoted[^2];
            // PNG bytes under the .bmp name: the image service reads a file by its content
            using var frame = new ImageService().Create(Size, format: ImageFormat.Png).GetAwaiter().GetResult();
            File.WriteAllBytes(FramePath, frame.GetBytes()!);
            return new ProcessOutput { ExitCode = ExitCode, Error = ExitCode == 0 ? null : "stand-in failure" };
        }

        public IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null)
            => throw new NotSupportedException();
    }
}
