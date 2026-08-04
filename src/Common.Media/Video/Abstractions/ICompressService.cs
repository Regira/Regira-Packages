using Regira.IO.Abstractions;
using Regira.Media.Video.Models;

namespace Regira.Media.Video.Abstractions;
public interface ICompressService
{
    Task<IMemoryFile?> Compress(IBinaryFile input, VideoSettings? options = null);
}
