using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services;
using Regira.Media.Drawing.Services.Abstractions;
using static Drawing.Testing.Extensions.DrawingTestHelpExtensions;

namespace Drawing.Testing.Extensions;

public static class ImageBuilderExtensions
{
    public static async Task Build_NoTarget(this IImageService service)
    {
        var imageLayers = new IImageLayer[]
        {
            new ImageLayer
            {
                Source = await service.ReadImage("yellow-200x150.jpg"),
                Options = new()
                {
                    Position = ImagePosition.HCenter | ImagePosition.VCenter,
                    Size = new ImageSize(300, 200)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("blue-50x50.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Top | ImagePosition.Right
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("red-150x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Bottom | ImagePosition.Left,
                    Size = new ImageSize(100,50)
                }
            }
        };

        var drawBuilder = new ImageBuilder(service, []);
        using var resultImg = await drawBuilder
            .Add(imageLayers)
            .Build();
        await service.SaveImage(resultImg, "build-no-target.jpg");

        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 10, 10), new[] { 10, 10 });
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 150, 100), new[] { 290, 60 });
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 290, 90), new[] { 290, 60 });
        AssertColor("#0000FF", await service.GetPixelColor(resultImg, 290, 10), new[] { 290, 10 });
        AssertColor("#0000FF", await service.GetPixelColor(resultImg, 260, 40), new[] { 260, 40 });
        AssertColor("#FF0000", await service.GetPixelColor(resultImg, 10, 160), new[] { 10, 160 });
        AssertColor("#FF0000", await service.GetPixelColor(resultImg, 90, 190), new[] { 90, 190 });
    }

    public static async Task Build_WithTargetCanvas(this IImageService service)
    {
        var target = new CanvasImageOptions
        {
            Size = new ImageSize(400, 300)
        };
        var imageLayers = new IImageLayer[]
        {
            new ImageLayer
            {
                Source = await service.ReadImage("yellow-200x150.jpg"),
                Options = new()
                {
                    Position = ImagePosition.HCenter | ImagePosition.VCenter,
                    Size = new ImageSize(350, 250)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("green-50x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Top | ImagePosition.Right,
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("blue-50x50.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Absolute,
                    Offset = new ImageEdgeOffset(20, null, null, 10)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("red-150x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Absolute,
                    Offset = new ImageEdgeOffset(null, 10, 20, null)
                }
            }
        };

        var imageCreators = new List<IImageCreator>
        {
            new CanvasImageCreator(service),
            new LabelImageCreator(service)
        };


        var drawBuilder = new ImageBuilder(service, imageCreators);
        using var resultImg = await drawBuilder.Add(imageLayers)
            .SetBaseLayer(target)
            .Build();
        await service.SaveImage(resultImg, "build-with-canvas.jpg");

        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 150, 299));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 390, 10));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 360, 90));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 45, 35));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 330, 35));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 45, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 350, 260));
        AssertColor("#FF0000", await service.GetPixelColor(resultImg, 20, 270));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 399, 299));
    }
    public static async Task Build_WithTargetImage(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imageLayers = new IImageLayer[]
        {
            new ImageLayer
            {
                Source = await service.ReadImage("yellow-200x150.jpg"),
                Options = new()
                {
                    Position = ImagePosition.HCenter | ImagePosition.VCenter,
                    Size = new ImageSize(350, 250)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("green-50x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Top | ImagePosition.Right,
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("blue-50x50.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Absolute,
                    Offset = new ImageEdgeOffset(20, null, null, 10)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("red-150x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Absolute,
                    Offset = new ImageEdgeOffset(null, 10, 20, null)
                }
            }
        };

        var imageCreators = new List<IImageCreator>
        {
            new CanvasImageCreator(service),
            new LabelImageCreator(service)
        };


        var drawBuilder = new ImageBuilder(service, imageCreators);
        using var resultImg = await drawBuilder
            .SetBaseLayer(target)
            .Add(imageLayers)
            .Build();
        await service.SaveImage(resultImg, "build-with-target.jpg");

        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 150, 299));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 390, 10));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 360, 90));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 45, 35));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 330, 35));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 45, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 350, 260));
        AssertColor("#FF0000", await service.GetPixelColor(resultImg, 20, 270));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 399, 299));
    }

    public static async Task Build_Images(this IImageService service)
    {
        using var target = await service.ReadImage("white-400x300.jpg");
        var imageLayers = new IImageLayer[]
        {
            new ImageLayer
            {
                Source = await service.ReadImage("yellow-200x150.jpg"),
                Options = new()
                {
                    Position = ImagePosition.HCenter | ImagePosition.VCenter,
                    Size = new ImageSize(350, 250)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("green-50x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Top | ImagePosition.Right,
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("blue-50x50.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Absolute,
                    Offset = new ImageEdgeOffset(20, null, null, 10)
                }
            },
            new ImageLayer
            {
                Source = await service.ReadImage("red-150x100.jpg"),
                Options = new()
                {
                    Position = ImagePosition.Absolute,
                    Offset = new ImageEdgeOffset(null, 10, 20, null)
                }
            },
            new ImageLayer<LabelImageOptions>
            {
                Source = new LabelImageOptions
                {
                    Text = "Hello World!",
                    FontName = "Arial",
                    FontSize = 25,
                    TextColor = "#000000",
                    BackgroundColor = "#FFFFFF50",
                    Padding = 5
                },
                Options = new()
                {
                    Position = ImagePosition.HCenter | ImagePosition.VCenter
                }
            }
        };

        var imageCreators = new List<IImageCreator>
        {
            new CanvasImageCreator(service),
            new LabelImageCreator(service)
        };


        var drawBuilder = new ImageBuilder(service, imageCreators);
        using var resultImg = await drawBuilder
            .SetBaseLayer(target)
            .Add(imageLayers)
            .Build();
        await service.SaveImage(resultImg, "build-images.jpg");

        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 10, 10));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 150, 299));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 390, 10));
        AssertColor("#00FF00", await service.GetPixelColor(resultImg, 360, 90));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 45, 35));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 330, 35));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 45, 160));
        AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 350, 260));
        //AssertColor("#FFFF00", await service.GetPixelColor(resultImg, 200, 150));
        AssertColor("#FF0000", await service.GetPixelColor(resultImg, 20, 270));
        AssertColor("#FFFFFF", await service.GetPixelColor(resultImg, 399, 299));
    }
}
