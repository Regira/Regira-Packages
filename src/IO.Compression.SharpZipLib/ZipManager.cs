using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;

namespace Regira.IO.Compression.SharpZipLib;

public class ZipManager
{
    public Stream Zip(IEnumerable<IBinaryFile> files, string? password = null)
    {
        var ms = new MemoryStream();
        var zs = new ZipOutputStream(ms);
        zs.Password = password;

        foreach (var file in files)
        {
            var ze = new ZipEntry(file.FileName);
            zs.PutNextEntry(ze);

            using var fileStream = file.GetStream()!;
            fileStream.Position = 0;
            StreamUtils.Copy(fileStream, zs, new byte[4096]);
        }

        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public async Task<BinaryFileCollection> Unzip(Stream zipStream, string? password = null)
    {
        var files = new List<BinaryFileItem>();

        var zs = new ZipInputStream(zipStream);
        zs.Password = password;

        while (zs.GetNextEntry() is { } entry)
        {
            if (entry.IsFile)
            {
                var data = new byte[entry.Size];
                var _ = await zs.ReadAsync(data, 0, data.Length);
                files.Add(new BinaryFileItem
                {
                    FileName = entry.Name,
                    Bytes = data
                });
            }
        }

        return new BinaryFileCollection(files);
    }
}