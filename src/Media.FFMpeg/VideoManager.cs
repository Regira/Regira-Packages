using FFMpegCore;
using FFMpegCore.Pipes;
using Regira.Dimensions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Video.Abstractions;
using Regira.Media.Video.Models;

namespace Regira.Media.FFMpeg;

public class VideoManager : ICompressService, IVideoService
{
    // https://github.com/rosenbjerg/FFMpegCore

    /// <summary>
    /// The VP9 quality setting (0–63, lower is better): 31 is the value recommended for 1080p
    /// </summary>
    private const int CONSTANT_RATE_FACTOR = 31;
    public async Task<VideoSettings?> GetInfo(IBinaryFile input)
    {
        // a file without a path is written to a temporary one for ffprobe, removed again below
        var inputIsTemporary = !input.HasPath();
        var inputPath = input.GetPath();
        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            var videoStream = mediaInfo.PrimaryVideoStream!;
            return new VideoSettings
            {
                FrameRate = (int)videoStream.FrameRate,
                Size = new Size2D(videoStream.Width, videoStream.Height)
            };
        }
        finally
        {
            if (inputIsTemporary)
            {
                TempFiles.TryDelete(inputPath);
            }
        }
    }

    public async Task<IMemoryFile?> Compress(IBinaryFile input, VideoSettings? options = null)
    {
        // a file without a path is written to a temporary one for ffmpeg, removed again below
        var inputIsTemporary = !input.HasPath();
        var inputPath = input.GetPath();
        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);

            // a setting left null comes from the source: half its size, and its own frame rate
            var size = options?.Size ?? new Size2D(mediaInfo.PrimaryVideoStream!.Width * .5f, mediaInfo.PrimaryVideoStream.Height * .5f);
            var frameRate = options?.FrameRate;

            var ms = new MemoryStream();
            await CreateCompressArguments(inputPath, new StreamPipeSink(ms), size, frameRate)
                .ProcessAsynchronously();

            ms.Seek(0, SeekOrigin.Begin);

            return ms.ToMemoryFile();
        }
        finally
        {
            if (inputIsTemporary)
            {
                TempFiles.TryDelete(inputPath);
            }
        }
    }

    protected internal virtual FFMpegArgumentProcessor CreateCompressArguments(string inputPath, IPipeSink output, Size2D size, int? frameRate)
        => FFMpegArguments
            .FromFileInput(inputPath)
            .OutputToPipe(output, o =>
            {
                o
                    .WithConstantRateFactor(CONSTANT_RATE_FACTOR)
                    .WithVideoCodec("vp9")
                    .ForceFormat("webm")
                    .WithVideoFilters(filterOptions =>
                    {
                        filterOptions.Scale((int)size.Width, (int)size.Height);
                    })
                    .WithFastStart();
                if (frameRate.HasValue)
                {
                    o.WithFramerate(frameRate.Value);
                }
            });
}
