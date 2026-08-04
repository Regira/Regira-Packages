using Regira.Collections;
using Regira.IO.Extensions;
using Regira.IO.Storage.FileSystem;
using Regira.Media.Drawing.Dimensions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Utilities;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.PDF.Testing.Abstractions;

public static class PdfTestHelper
{
    static readonly string AssemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
    static readonly string AssetsDir = Path.Combine(AssemblyDir, "../../../", "Assets");
    private static string InputDir => Path.Combine(AssetsDir, "Input");
    static string OutputDir(object service) => Path.Combine(AssetsDir, "Output", service.GetType().Assembly.GetName().Name!.Split('.').Last());

    public static async Task Split_Documents(IPdfSplitter pdfSplitter, string inputFileName = "lorem-24-pages.pdf")
    {
        var outputDir = OutputDir(pdfSplitter);
        Directory.CreateDirectory(outputDir);

        var pdfPath = Path.Combine(InputDir, inputFileName);
        await using var pdfStream = File.OpenRead(pdfPath);
        var pageCount = await pdfSplitter.GetPageCount(pdfStream.ToBinaryFile());
        var ranges = new PdfSplitRange[]
        {
            new() { Start = 1, End = (int)Math.Floor(pageCount / 2d) },
            new() { Start = (int)Math.Ceiling(pageCount / 2d) + 1, End = pageCount - 1 }
        };
        var splidPdfs = (await pdfSplitter.Split(pdfStream.ToBinaryFile(), ranges)).ToArray();
        Assert.That(splidPdfs.Length, Is.EqualTo(ranges.Length));
        for (var i = 0; i < splidPdfs.Length; i++)
        {
            var splitPdf = splidPdfs[i];
            var splitPageCount = await pdfSplitter.GetPageCount(splitPdf.ToBinaryFile());
            Assert.That(splitPageCount, Is.EqualTo(ranges[i].End - ranges[i].Start + 1));
            await FileSystemUtility.SaveStream(Path.Combine(outputDir, $"split-{i + 1}.pdf"), splitPdf.GetStream()!);
        }
        // clean up
        foreach (var splitStream in splidPdfs)
        {
            splitStream.Dispose();
        }
    }
    public static async Task Merge_Split_Documents(IPdfSplitter pdfSplitter, IPdfMerger pdfMerger, string inputFileName = "lorem-24-pages.pdf")
    {
        var inputDir = Path.Combine(AssetsDir, "Input");
        var outputDir = OutputDir(pdfMerger);
        Directory.CreateDirectory(outputDir);

        var pdfPath = Path.Combine(inputDir, inputFileName);
        await using var pdfStream = File.OpenRead(pdfPath);
        var pageCount = await pdfSplitter.GetPageCount(pdfStream.ToBinaryFile());
        var ranges = new PdfSplitRange[]
            {
                new() { Start = 2, End = 4 },
                new() { Start = 6, End = 7 },
                new() { Start = 9, End = 10 },
                new() { Start = 12, End = 15 },
                new() { Start = 16, End = 20 },
                new() { Start = 22 }
            }
            .Where(r => r.Start < pageCount && (r.End ?? 0) <= pageCount)
            .ToArray();
        var splitPdfs = (await pdfSplitter.Split(pdfStream.ToBinaryFile(), ranges))
            .Select(x => x.ToBinaryFile())
            .ToArray();

        using var merged = await pdfMerger.Merge(splitPdfs);
        var expectedPageCount = ranges.Sum(r => (r.End ?? pageCount) - r.Start + 1);
        var mergedPageCount = await pdfSplitter.GetPageCount(merged!.ToBinaryFile());

        await FileSystemUtility.SaveStream(Path.Combine(outputDir, "split-merged.pdf"), merged!.GetStream()!);
        Assert.That(mergedPageCount, Is.EqualTo(expectedPageCount));
        // clean up
        splitPdfs.Dispose();
    }

    public static async Task ToImages(IPdfToImageService service, string inputFileName = "sample.pdf")
    {
        var inputDir = Path.Combine(AssetsDir, "Input");
        var outputDir = OutputDir(service);
        Directory.CreateDirectory(outputDir);

        var pdfPath = Path.Combine(inputDir, inputFileName);
        await using var pdfStream = File.OpenRead(pdfPath);
        ImageSize size = new[] { 480, 600 };
        var images = await service.ToImages(pdfStream.ToBinaryFile(), new PdfToImagesOptions { Size = size });

        var count = 0;
        foreach (var image in images)
        {
            Assert.That(image, Is.Not.Null);
            var imgPath = Path.Combine(outputDir, $"pdf-img-{count + 1}.jpg");
            await File.WriteAllBytesAsync(imgPath, image.Bytes!);
            //Assert.IsTrue(size.Width >= image.Size.Width);
            //Assert.AreEqual(size.Height, image.Size.Height);
            count++;
        }

        Assert.That(count, Is.EqualTo(2));
    }
}