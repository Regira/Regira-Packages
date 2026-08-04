using Regira.Dimensions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Media.Drawing.Utilities;
using Regira.Office.OCR.PaddleOCR;
using Regira.Utilities;

namespace Drawing.Testing.Extensions;

public static class DrawingTestHelpExtensions
{
    internal static async Task SetupTests(this IImageService service)
    {
        var inputDir = service.GetInputDir();
        var transparentPath = Path.Combine(inputDir, "transparent-400x300.jpg");
        if (!File.Exists(transparentPath))
        {
            using var img = await service.Create(new ImageSize(400, 300), null, ImageFormat.Png);
            await img.SaveAs(transparentPath);
        }
        var whitePath = Path.Combine(inputDir, "white-400x300.jpg");
        if (!File.Exists(whitePath))
        {
            using var img = await service.Create(new ImageSize(400, 300), "#FFFFFF", ImageFormat.Jpeg);
            await img.SaveAs(whitePath);
        }
        var yellowPath = Path.Combine(inputDir, "yellow-200x150.jpg");
        if (!File.Exists(yellowPath))
        {
            using var img = await service.Create(new ImageSize(200, 150), "#FFFF00", ImageFormat.Jpeg);
            await img.SaveAs(yellowPath);
        }
        var redPath = Path.Combine(inputDir, "red-150x100.jpg");
        if (!File.Exists(redPath))
        {
            using var img = await service.Create(new ImageSize(150, 100), "#FF0000", ImageFormat.Jpeg);
            await img.SaveAs(redPath);
        }
        var greenPath = Path.Combine(inputDir, "green-50x100.jpg");
        if (!File.Exists(greenPath))
        {
            using var img = await service.Create(new ImageSize(50, 100), "#00FF00", ImageFormat.Jpeg);
            await img.SaveAs(greenPath);
        }
        var bluePath = Path.Combine(inputDir, "blue-50x50.jpg");
        if (!File.Exists(bluePath))
        {
            using var img = await service.Create(new ImageSize(50, 50), "#0000FF", ImageFormat.Jpeg);
            await img.SaveAs(bluePath);
        }
    }

    internal static string GetAssetsDir(this IImageService service)
        => Path.Combine(AssemblyUtility.GetAssemblyDirectory()!, "../../../", "Assets");
    internal static string GetLibName(this IImageService service)
        => service.GetType().Assembly.GetName().Name!.Split('.').Last();
    internal static string GetInputDir(this IImageService service)
        => Path.Combine(service.GetAssetsDir(), "Input", service.GetLibName());
    internal static string GetOutputDir(this IImageService service)
        => Path.Combine(service.GetAssetsDir(), "Output", service.GetLibName());

    internal static async Task<IImageFile> ReadImage(this IImageService service, string filename)
    {
        var path = Path.Combine(service.GetInputDir(), filename);
        if (!File.Exists(path))
        {
            path = Path.Combine(service.GetAssetsDir(), "Input", filename);
        }
        var bytes = await File.ReadAllBytesAsync(path);

        var img = bytes.ToBinaryFile().ToImageFile();
        img.Size = await service.GetDimensions(img);
        return img;
    }
    internal static async Task<string?> ReadImageText(this IImageFile img)
    {
        var result = await new OcrManager().Read(img);
        return result.Text;
    }

    internal static async Task SaveImage(this IImageService service, IImageFile file, string filename)
        => await file.SaveAs(Path.Combine(service.GetOutputDir(), filename));

    internal static void AssertColor(Color expected, Color actual, Position2D? pos = null)
    {
        Assert.That(Math.Abs(actual.Red - expected.Red), Is.LessThan(15), $"expected {expected} vs actual {actual} at {pos}");
        Assert.That(Math.Abs(actual.Green - expected.Green), Is.LessThan(15), $"expected {expected} vs actual {actual} at {pos}");
        Assert.That(Math.Abs(actual.Blue - expected.Blue), Is.LessThan(15), $"expected {expected} vs actual {actual} at {pos}");
    }
}
