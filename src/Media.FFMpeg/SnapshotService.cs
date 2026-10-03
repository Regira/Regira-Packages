using FFMpegCore;
using System.Globalization;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Media.Drawing.Utilities;
using Regira.System;
using Regira.System.Abstractions;

namespace Regira.Media.FFMpeg;

public class SnapshotService(IImageService imageService, IProcessHelper? processHelper = null)
{
    readonly IProcessHelper _processHelper = processHelper ?? new ProcessHelper();

    public async Task<IImageFile?> Snapshot(IBinaryFile input, ImageSize? size = null, TimeSpan? time = null, CancellationToken cancellationToken = default)
    {
        // a file without a path is written to a temporary one for ffmpeg, removed again below
        var inputIsTemporary = !input.HasPath();
        var inputPath = input.GetPath();
        var framePath = Path.Combine(Path.GetTempPath(), $"regira-snapshot-{Guid.NewGuid():N}.bmp");
        try
        {
            if (!size.HasValue || size.Value.Width == 0 || size.Value.Height == 0)
            {
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                size = new ImageSize(mediaInfo.PrimaryVideoStream!.Width, mediaInfo.PrimaryVideoStream!.Height);
            }

            // ffmpeg reads the position in seconds; without one it takes the first frame. Before -i it seeks the input
            // rather than decoding everything up to the position, and stays frame-accurate since it re-encodes the frame
            var seek = time.HasValue
                ? $"-ss {time.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} "
                : string.Empty;
            var cmd = $@"ffmpeg {seek}-i ""{inputPath}"" -update 1 -frames:v 1 ""{framePath}""";
            // capture what ffmpeg has to say: it reports every failure on stderr, and without it there is only an exit code
            var result = _processHelper.ExecuteCommand(cmd, waitForOutput: true);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Internal error while creating snapshot (ExitCode {result.ExitCode}): {result.Error}");
            }

            using var frameFile = new BinaryFileItem(framePath);
            using var img = frameFile.ToImageFile();
            if (img.Length <= 0)
            {
                throw new Exception("Empty file");
            }
            using var jpeg = await imageService.ChangeFormat(img, Drawing.Enums.ImageFormat.Jpeg, cancellationToken);
            return await imageService.Resize(jpeg, size.Value, cancellationToken: cancellationToken);
        }
        finally
        {
            TempFiles.TryDelete(framePath);
            if (inputIsTemporary)
            {
                TempFiles.TryDelete(inputPath);
            }
        }
    }
}
