using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Services.Abstractions;
using static Drawing.Testing.Extensions.DrawingTestHelpExtensions;

namespace Drawing.Testing.Extensions;

public static class DrawingPositionExtensions
{
    public static async Task AddImage_No_Params(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg")
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-no-params.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 190, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 210, 150));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 200, 160));
    }

    // Positioned
    public static async Task AddImage_Top_Left(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Top | ImagePosition.Left
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-top-left.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 190, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 210, 150));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 200, 160));
    }
    public static async Task AddImage_Bottom_Left(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Bottom | ImagePosition.Left
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-bottom-left.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 10, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 190, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 210, 290));
    }
    public static async Task AddImage_Top_Right(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("green-50x100.jpg"),
            Options = new()
            {
                Position = ImagePosition.Top | ImagePosition.Right
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-top-right.jpg");

        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 360, 10));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 390, 90));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 340, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 360, 110));
    }
    public static async Task AddImage_Bottom_Right(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("green-50x100.jpg"),
            Options = new()
            {
                Position = ImagePosition.Bottom | ImagePosition.Right
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-bottom-right.jpg");

        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 360, 210));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 390, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 390, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 390, 190));
    }

    // Centered
    public static async Task AddImage_Top_HCenter(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Top | ImagePosition.HCenter
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-top-hcenter.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 110, 10));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 290, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 200, 160));
    }
    public static async Task AddImage_Bottom_HCenter(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Bottom | ImagePosition.HCenter
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-bottom-hcenter.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 110, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 290, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 90, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 310, 290));
    }
    public static async Task AddImage_Left_VCenter(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("green-50x100.jpg"),
            Options = new()
            {
                Position = ImagePosition.VCenter | ImagePosition.Left
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-right-vcenter.jpg");

        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 10, 110));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 10, 190));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 90));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 60, 150));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 290));
    }
    public static async Task AddImage_Right_VCenter(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("green-50x100.jpg"),
            Options = new()
            {
                Position = ImagePosition.VCenter | ImagePosition.Right
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-right-vcenter.jpg");

        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 360, 110));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 390, 190));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 390, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 340, 150));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 390, 290));
    }
    public static async Task AddImage_Middle(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.VCenter | ImagePosition.HCenter
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-middle.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 110, 85));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 200, 150));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 290, 175));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 150));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 200));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 90, 150));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 310, 150));
    }

    // Absolute
    public static async Task AddImage_Absolute_Top_Left(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Absolute,
                Offset = new ImageEdgeOffset(50, 50)
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-abs-top-left.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 80, 80)); // GDI causes problems when checking (60, 60)
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 240, 190));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 40, 40));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 260, 210));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 390, 290));
    }
    public static async Task AddImage_Absolute_Top_Right(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Absolute,
                Offset = new ImageEdgeOffset(50, null, null, 50)
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-abs-top-right.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 160, 60));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 240, 190));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 140, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 140, 60));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 360, 60));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 360, 190));
    }
    public static async Task AddImage_Absolute_Bottom_Left(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Absolute,
                Offset = new ImageEdgeOffset(null, 50, 50, null)
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-abs-bottom-left.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 60, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 240, 240));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 40, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 260, 260));
    }
    public static async Task AddImage_Absolute_Bottom_Right(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Absolute,
                Offset = new ImageEdgeOffset(null, null, 50, 50)
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-abs-bottom-right.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 160, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 240, 240));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 140, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 360, 260));
    }

    // Margins
    public static async Task AddImage_Margin10(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Margin = 10
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-margin10.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 20, 20));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 190, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 1, 1));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 220, 170));
    }
    public static async Task AddImage_Top_Left_Margin10(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Top | ImagePosition.Left,
                Margin = 10
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-top-left-margin10.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 20, 20));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 190, 140));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 1, 1));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 220, 170));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 220, 170));
    }
    public static async Task AddImage_Bottom_Left_Margin10(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("yellow-200x150.jpg"),
            Options = new()
            {
                Position = ImagePosition.Bottom | ImagePosition.Left,
                Margin = 10
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-bottom-left-margin10.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 20, 150));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 200, 280));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 130));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 220, 290));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 0, 299));
    }
    public static async Task AddImage_Top_Right_Margin10(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("green-50x100.jpg"),
            Options = new()
            {
                Position = ImagePosition.Top | ImagePosition.Right,
                Margin = 10
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-top-right-margin10.jpg");

        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 350, 20));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 380, 100));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 330, 20));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 380, 120));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 399, 0));
    }
    public static async Task AddImage_Bottom_Right_Margin10(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imgToAdd = new ImageLayer
        {
            Source = await service.ReadImage("green-50x100.jpg"),
            Options = new()
            {
                Position = ImagePosition.Bottom | ImagePosition.Right,
                Margin = 10
            }
        };

        using var resultImg = await service.Draw([imgToAdd], target);
        await service.SaveImage(resultImg, "add-image-bottom-right-margin10.jpg");

        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 350, 200));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 380, 280));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 280));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 330, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 380, 180));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 399, 299));
    }
}