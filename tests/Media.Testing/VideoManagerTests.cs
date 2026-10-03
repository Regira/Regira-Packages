using FFMpegCore.Pipes;
using Regira.Dimensions;
using Regira.Media.FFMpeg;

namespace Media.Testing;

// The ffmpeg arguments VideoManager composes for Compress; nothing is run, so no ffmpeg is needed.
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class VideoManagerTests
{
    private static string CompressArguments(int? frameRate)
        => new ArgumentsProbe().Arguments(new Size2D(640, 360), frameRate);

    [Test]
    public void FrameRate_Sets_The_Output_Frame_Rate()
    {
        var arguments = CompressArguments(24);

        Assert.That(arguments, Does.Contain("-r 24"));
    }

    [Test]
    public void FrameRate_Leaves_The_Quality_Alone()
    {
        Assert.That(CompressArguments(24), Does.Contain("-crf 31"));
        Assert.That(CompressArguments(60), Does.Contain("-crf 31"));
    }

    [Test]
    public void Without_A_FrameRate_Keeps_The_Source_Frame_Rate()
    {
        var arguments = CompressArguments(null);

        Assert.That(arguments, Does.Not.Contain("-r "));
    }

    [Test]
    public void Scales_To_The_Size()
    {
        var arguments = CompressArguments(null);

        Assert.That(arguments, Does.Contain("scale=640:360"));
    }

    private sealed class ArgumentsProbe : VideoManager
    {
        public string Arguments(Size2D size, int? frameRate)
            => CreateCompressArguments("input.mp4", new StreamPipeSink(new MemoryStream()), size, frameRate).Arguments;
    }
}
