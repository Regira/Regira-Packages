using Regira.IO.Abstractions;
using Regira.Media.Video.Models;

namespace Regira.Media.Video.Abstractions;

public interface IVideoService
{
    Task<VideoSettings?> GetInfo(IBinaryFile input);
}
