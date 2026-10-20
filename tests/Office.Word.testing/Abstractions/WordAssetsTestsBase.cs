using System.Text.Json;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Utilities;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// Asset and output paths shared by every Word fixture: the backend fixtures through
/// <see cref="WordTestsBase"/>, and fixtures that exercise a helper rather than a backend directly.
/// </summary>
public abstract class WordAssetsTestsBase
{
    private static readonly JsonSerializerOptions FactsJson = new() { WriteIndented = true, IncludeFields = true };

    protected readonly string InputDir;
    protected readonly string OutputDir;

    protected WordAssetsTestsBase(string outputFolderName)
    {
        InputDir = Path.Combine(AssetsDirectory, "Input");
        // Fixtures run in parallel (see Properties/AssemblyInfo.cs), so each one writes to a folder of its own.
        OutputDir = Path.Combine(OutputDirectory, outputFolderName);
        Directory.CreateDirectory(OutputDir);
    }

    /// <summary>The project's <c>Assets</c> folder, beside the source rather than the build output.</summary>
    public static string AssetsDirectory { get; } = Path.GetFullPath(Path.Combine(AssemblyUtility.GetAssemblyDirectory()!, "../../../", "Assets"));
    public static string OutputDirectory => Path.Combine(AssetsDirectory, "Output");

    protected string InputPath(string filename) => Path.Combine(InputDir, filename);

    protected IBinaryFile ReadAsset(string filename) => File.ReadAllBytes(InputPath(filename)).ToBinaryFile();

    protected WordTemplateInput TemplateInput(string filename) => new() { Template = ReadAsset(filename) };

    /// <summary>
    /// Saves a produced .docx under the running test's name and reads it with <see cref="DocxFacts"/>, recording the
    /// facts beside it.
    /// </summary>
    /// <param name="suffix">Tells apart several documents of one test</param>
    protected Task<DocxFacts> ReadDocx(IMemoryFile output, string? suffix = null)
        => Record(output, "docx", suffix, DocxFacts.Read);

    /// <summary>Saves a produced PDF under the running test's name and reads it with <see cref="PdfFacts"/>.</summary>
    protected Task<PdfFacts> ReadPdf(IMemoryFile output, string? suffix = null)
        => Record(output, "pdf", suffix, PdfFacts.Read);

    /// <summary>Saves a rendered page under the running test's name and reads it with <see cref="ImageFacts"/>.</summary>
    protected Task<ImageFacts> ReadImage(IMemoryFile image, string? suffix = null)
        => Record(image, Facts.Sniff(image.GetBytes()!) == FileFormat.Png ? "png" : "jpg", suffix, ImageFacts.Read);

    /// <summary>Saves a produced file under the running test's name, for a reader to open; returns its path.</summary>
    protected async Task<string> Save(IMemoryFile output, string extension, string? suffix = null)
    {
        var path = TestOutputPath(extension, suffix);
        await output.SaveAs(path);
        TestContext.AddTestAttachment(path);
        return path;
    }

    protected static string Extension(FileFormat format) => format == FileFormat.Jpeg ? "jpg" : format.ToString().ToLowerInvariant();

    private async Task<TFacts> Record<TFacts>(IMemoryFile output, string extension, string? suffix, Func<IMemoryFile, TFacts> read)
    {
        var path = await Save(output, extension, suffix);
        var facts = read(output);
        var factsPath = $"{path}.facts.json";
        await File.WriteAllTextAsync(factsPath, JsonSerializer.Serialize(facts, FactsJson));
        TestContext.AddTestAttachment(factsPath);
        return facts;
    }

    /// <summary><c>{OutputDir}/{test name}{suffix}.{extension}</c>, with the characters a file name cannot hold replaced.</summary>
    private string TestOutputPath(string extension, string? suffix)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat($"{TestContext.CurrentContext.Test.Name}{suffix}".Where(c => c != '"').Select(c => invalid.Contains(c) ? '_' : c));
        return Path.Combine(OutputDir, $"{name}.{extension}");
    }
}
