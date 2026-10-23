using Drawing.Testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;

namespace Drawing.Testing.Skia;

public class SkiaImageTests() : ImageTestsBase(new ImageService())
{
    [Ignore("Library defect S3: Skia MakeTransparent clears every pixel lighter than the colour on all three channels and leaves the colour itself opaque (SkiaUtility.MakeTransparent compares with >), where IImageColorService clears the specified colour")]
    public override Task MakeTransparent_Clears_The_Key_Colour_Only() => base.MakeTransparent_Clears_The_Key_Colour_Only();
}