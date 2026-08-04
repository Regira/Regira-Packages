using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Services.Abstractions;

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

    public static async Task Add_Text_With_Margin(this IImageService service)
    {
        var input = "Hello World!";
        using var testImage = await service.CreateTextImage(new LabelImageOptions
        {
            Text = input,
            FontSize = 25,
            FontName = "Arial",
            TextColor = "#00F",
            BackgroundColor = "#FFFF0099",
            Padding = 5,
        });
        await service.SaveImage(testImage, "hello-world_margin.png");
        Assert.That(testImage.Format, Is.EqualTo(ImageFormat.Png));
        var content = await testImage.ReadImageText();
        Assert.That(content, Is.EqualTo(input));
        DrawingTestHelpExtensions.AssertColor("#FFFF00", await service.GetPixelColor(testImage, 1, 1));
    }
}