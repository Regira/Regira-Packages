using Drawing.Testing.Abstractions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Services.Abstractions;
using SkiaSharp;

namespace Drawing.Testing.Extensions;

public static class DrawingTextExtensions
{
    public static async Task Add_Text_No_Params(this IImageService service)
    {
        var input = "Hello World!";
        using var testImage = await service.CreateTextImage(input);

        await service.SaveImage(testImage, "hello-world.png");
        Assert.That(testImage.Format, Is.EqualTo(ImageFormat.Png));

        var content = await testImage.ReadImageText();

        Assert.That(content, Is.EqualTo(input));
        DrawingTestHelpExtensions.AssertColor("#FFFFFF", await service.GetPixelColor(testImage, 1, 1));
    }

    public static async Task Add_Text_With_Options(this IImageService service)
    {
        var input = "Hello World!";
        using var testImage = await service.CreateTextImage(new LabelImageOptions
        {
            Text = input,
            FontSize = 25,
            FontName = "Arial",
            TextColor = "#00F",
            BackgroundColor = "#FFFF0099",
        });

        await service.SaveImage(testImage, "hello-world_options.png");
        Assert.That(testImage.Format, Is.EqualTo(ImageFormat.Png));

        var content = await testImage.ReadImageText();

        Assert.That(content, Is.EqualTo(input));
        DrawingTestHelpExtensions.AssertColor("#FFFF00", await service.GetPixelColor(testImage, 1, 1));
    }

    /// <summary>
    /// Padding frames the label of <see cref="Add_Text_With_Options"/>, which reads that label back by OCR: the canvas
    /// grows by the padding on every side, the frame is the background colour, and inside it sits the unpadded label,
    /// pixel for pixel.
    /// </summary>
    public static async Task Add_Text_With_Margin(this IImageService service)
    {
        const int padding = 5;
        var options = new LabelImageOptions
        {
            Text = "Hello World!",
            FontSize = 25,
            FontName = "Arial",
            TextColor = "#00F",
            BackgroundColor = "#FFFF0099",
        };
        using var unpaddedImage = await service.CreateTextImage(options);
        options.Padding = padding;
        using var testImage = await service.CreateTextImage(options);
        await service.SaveImage(testImage, "hello-world_margin.png");
        Assert.That(testImage.Format, Is.EqualTo(ImageFormat.Png));
        DrawingTestHelpExtensions.AssertColor("#FFFF00", await service.GetPixelColor(testImage, 1, 1));

        using var label = ImageReadBack.Decode(unpaddedImage);
        using var padded = ImageReadBack.Decode(testImage);
        // the label's corner, above the text; a premultiplied surface rounds a translucent colour by up to one level
        var background = label.GetPixel(0, 0);
        Assert.That(new[] { background.Red, background.Green, background.Blue, background.Alpha }.Select(c => (int)c),
            Is.EqualTo(new[] { 0xFF, 0xFF, 0x00, 0x99 }).Within(1).AsCollection, "the background");
        Assert.That((padded.Width, padded.Height), Is.EqualTo((label.Width + 2 * padding, label.Height + 2 * padding)));
        var inFrame = new List<SKColor>();
        var outOfPlace = new List<(int X, int Y)>();
        for (var y = 0; y < padded.Height; y++)
        {
            for (var x = 0; x < padded.Width; x++)
            {
                var (labelX, labelY) = (x - padding, y - padding);
                if (labelX < 0 || labelY < 0 || labelX >= label.Width || labelY >= label.Height)
                {
                    inFrame.Add(padded.GetPixel(x, y));
                }
                else if (padded.GetPixel(x, y) != label.GetPixel(labelX, labelY))
                {
                    outOfPlace.Add((x, y));
                }
            }
        }
        Assert.Multiple(() =>
        {
            Assert.That(inFrame, Is.All.EqualTo(background), "the frame");
            Assert.That(outOfPlace, Is.Empty, "the label inside the frame");
        });
    }
}