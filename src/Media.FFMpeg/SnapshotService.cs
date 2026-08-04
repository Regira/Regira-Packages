using FFMpegCore;
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
        var inputPath = input.GetPath();
        if (!size.HasValue || size.Value.Width == 0 || size.Value.Height == 0)
        {
            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            size = new ImageSize(mediaInfo.PrimaryVideoStream!.Width, mediaInfo.PrimaryVideoStream!.Height);
        }

        var tempPath = $"{Path.GetTempFileName()}.bmp";
        var ss = time?.ToString().Substring(0, 12);
        var cmd = $@"ffmpeg -i ""{inputPath}"" -ss {ss} -update 1 -frames:v 1 ""{tempPath}""";
        var result = _processHelper.ExecuteCommand(cmd);

        //var success = await FFMegService.SnapshotAsync(inputPath, tempFile, new Size((int)size.Value.Width, (int)size.Value.Height), time);
        if (result.ExitCode != 0)
        {
            throw new Exception("Internal error while creating snapshot");
        }

        using var tempFile = new BinaryFileItem(tempPath);
        using var img = tempFile.ToImageFile();
        if (img.Length <= 0)
        {
            throw new Exception("Empty file");
        }
        using var jpeg = await imageService.ChangeFormat(img, Drawing.Enums.ImageFormat.Jpeg, cancellationToken);
        var resized = await imageService.Resize(jpeg, size.Value, cancellationToken: cancellationToken);

        File.Delete(tempPath);

        return resized;
    }
}
