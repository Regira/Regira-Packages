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

    private const int DEFAULT_FRAMERATE = 30;
    public async Task<VideoSettings?> GetInfo(IBinaryFile input)
    {
        var inputPath = input.GetPath();
        var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
        var videoStream = mediaInfo.PrimaryVideoStream!;
        return new VideoSettings
        {
            FrameRate = (int)videoStream.FrameRate,
            Size = new Size2D(videoStream.Width, videoStream.Height)
        };
    }

    public async Task<IMemoryFile?> Compress(IBinaryFile input, VideoSettings? options = null)
    {
        options ??= new();

        var inputPath = input.GetPath();
        var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
        VideoStream? videoStream = null;
        if (options.FrameRate.HasValue != true || options.Size.HasValue != true)
        {
            videoStream = mediaInfo.PrimaryVideoStream!;
        }

        options.FrameRate ??= (int)(videoStream!.FrameRate * .9);
        options.Size ??= new Size2D(videoStream!.Width * .5f, videoStream.Height * .5f);

        var size = options.Size;
        var frameRate = options.FrameRate ?? DEFAULT_FRAMERATE;

        var ms = new MemoryStream();
        var outputPipe = new StreamPipeSink(ms);

        await FFMpegArguments
            .FromFileInput(inputPath)
            .OutputToPipe(outputPipe, o =>
            {
                o
                    .WithConstantRateFactor(frameRate)
                    .WithVideoCodec("vp9")
                    .ForceFormat("webm")
                    .WithVideoFilters(filterOptions =>
                    {
                        filterOptions.Scale((int)size.Value.Width, (int)size.Value.Height);
                    })
                    .WithFastStart();
            })
            .ProcessAsynchronously();

        ms.Seek(0, SeekOrigin.Begin);

        return ms.ToMemoryFile();
    }
}
