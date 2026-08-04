using System.IO.Compression;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;

namespace Regira.IO.Storage.Compression;

public class ZipBuilder
{
    private IEnumerable<BinaryFileItem>? _files;
    private Stream? _zipStream;

    public ZipBuilder For(Stream? zipStream = null)
    {
        _zipStream = zipStream;
        return this;
    }
    public ZipBuilder For(IEnumerable<BinaryFileItem> files)
    {
        _files = files;
        return this;
    }
    public Task<IMemoryFile> Build()
    {
        if (_files == null)
        {
            throw new NullReferenceException($"Invoke {nameof(For)}(files) first");
        }

        if (_zipStream != null)
        {
            ZipUtility.AddFiles(_zipStream, _files);
            return Task.FromResult(_zipStream.ToMemoryFile());
        }

        var zipStream = new MemoryStream();
        using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Update, true);
        foreach (var file in _files)
        {
            zipArchive.AddFile(file);
        }
        
        return Task.FromResult(zipStream.ToMemoryFile());
    }
}