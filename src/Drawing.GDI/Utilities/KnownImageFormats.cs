using System.Drawing.Imaging;
using System.Reflection;

namespace Regira.Drawing.GDI.Utilities;

public class KnownImageFormats
{
    //https://stackoverflow.com/questions/8489705/c-sharp-imageformat-to-string#8489751
    private static readonly Dictionary<Guid, string> Data = typeof(ImageFormat)
        .GetProperties(BindingFlags.Static | BindingFlags.Public)
        .Where(p => p.PropertyType == typeof(ImageFormat))
        .Select(p =>
        {
            var value = (ImageFormat)p.GetValue(null, null)!;
            return new { value.Guid, Name = value.ToString() };
        })
        .ToDictionary(p => p.Guid, p => p.Name);

    public string this[Guid key] => Data[key];

    public bool ContainsKey(Guid key) => Data.ContainsKey(key);
    public bool TryGetValue(Guid key, out string? value) => Data.TryGetValue(key, out value);


    public string GetMimeType(ImageFormat format)
    {
        if (TryGetValue(format.Guid, out var name))
        {
            return $"image/{name}";
        }

        throw new NotSupportedException($"ImageFormat {format.Guid} not known");
    }
}