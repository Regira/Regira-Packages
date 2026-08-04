using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Utilities;

namespace Regira.Media.Drawing.Utilities;

public static class ImageFileUtility
{
    public static void Load(IImageFile img, string path)
    {
        var bytes = File.ReadAllBytes(path);
        img.Bytes = bytes;
        img.Length = bytes.Length;
        img.ContentType = ContentTypeUtility.GetContentType(path);
    }
    public static ImageFile ToImageFile(this IBinaryFile file)
    {
        if (!file.HasBytes() && !file.HasStream())
        {
            if (file.HasPath())
            {
                var img = new ImageFile();
                Load(img, file.Path!);
                return img;
            }
        }
        return new ImageFile
        {
            Bytes = file.Bytes,
            Stream = file.Stream,
            Length = file.GetLength(),
            ContentType = file.ContentType,
        };
    }


    public static string ToBase64(IMemoryFile file, string? contentType = null)
    {
        var bytes = file.GetBytes();
        if (bytes?.Any() != true)
        {
            throw new Exception("Could not get contents of file");
        }

        contentType ??= file.ContentType ?? "image/png";
        return $"data:{contentType};base64,{bytes.GetBase64String()}";
    }
    public static ImageFile FromBase64(string contents)
    {
        string? contentType = null;
        var firstChars = contents.Truncate(64)!;
        if (firstChars.Contains(','))
        {
            contentType = firstChars.Split(';').FirstOrDefault()?.Split(':').LastOrDefault();
        }

        var bytes = FileUtility.GetBytes(contents);
        return new ImageFile
        {
            Bytes = bytes,
            Length = bytes.Length,
            ContentType = contentType,
        };
    }
}