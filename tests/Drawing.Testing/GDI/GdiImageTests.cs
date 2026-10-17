using System.Drawing;
using System.Drawing.Imaging;
using Drawing.Testing.Abstractions;
using Regira.Drawing.GDI.Services;
using Regira.Drawing.GDI.Utilities;

namespace Drawing.Testing.GDI;

public class GdiImageTests() : ImageTestsBase(new ImageService())
{
    /// <summary>An indexed image — a GIF, an 8-bit BMP — rotates onto a canvas GDI+ can draw on.</summary>
    [Test]
    public async Task Rotate_An_Indexed_Image()
    {
        using var gif = new Bitmap(40, 20, PixelFormat.Format8bppIndexed);
        using var file = gif.ToImageFile(ImageFormat.Gif);

        using var rotated = await new ImageService().Rotate(file, 45);

        Assert.That(rotated.Size!.Value.Width, Is.GreaterThan(40));
    }
}
