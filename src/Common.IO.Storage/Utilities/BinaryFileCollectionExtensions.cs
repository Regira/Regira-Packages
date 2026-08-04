using Regira.IO.Models;
using Regira.IO.Storage.FileSystem;
using Regira.IO.Storage.Helpers;

namespace Regira.IO.Storage.Utilities;

public enum FileDataSource
{
    Path,
    Bytes,
    Stream
}

public static class BinaryFileCollectionExtensions
{
    public static async Task<BinaryFileCollection> ToBinaryFileCollection(this IEnumerable<string> files, FileDataSource source = FileDataSource.Path)
    {
        var items = new List<BinaryFileItem>();
        var fileList = files.ToArray();

        var baseFolder = FileNameHelper.GetBaseFolder(fileList);

        foreach (var file in fileList)
        {
            var item = new BinaryFileItem
            {
                FileName = FileNameUtility.GetRelativeUri(file, baseFolder),
                Path = file
            };
            if (source == FileDataSource.Stream)
            {
                item.Stream = File.OpenRead(file);
            }
            else if (source == FileDataSource.Bytes)
            {
                item.Bytes = await File.ReadAllBytesAsync(file);
            }

            items.Add(item);
        }

        return new BinaryFileCollection(items);
    }
}