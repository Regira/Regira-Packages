using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework.Interfaces;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Utilities;

namespace Office.PDF.Testing.Abstractions;

/// <summary>
/// Asset and output paths shared by every PDF fixture, and the recording of what a scenario produced: each file is
/// saved under the running test's name in the backend's folder, with the facts it was read for beside it.
/// </summary>
public abstract class PdfAssetsTestsBase
{
    private static readonly JsonSerializerOptions FactsJson = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    // The running test's outputs, attached to its result only when it fails: a green run with a logger copies none of
    // them into TestResults. The tests of one fixture run one at a time (see Properties/AssemblyInfo.cs).
    private readonly List<string> _outputs = [];

    [TearDown]
    public void AttachOutputsOfAFailedTest()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            foreach (var path in _outputs)
            {
                TestContext.AddTestAttachment(path);
            }
        }
        _outputs.Clear();
    }

    protected readonly string OutputDir;

    /// <param name="outputFolderName">The backend's folder under <c>Assets/Output</c>.</param>
    protected PdfAssetsTestsBase(string outputFolderName)
    {
        // Fixtures run in parallel (see Properties/AssemblyInfo.cs), so each one writes to a folder of its own.
        OutputDir = Path.Combine(OutputDirectory, outputFolderName);
        Directory.CreateDirectory(OutputDir);
    }

    /// <summary>The project's <c>Assets</c> folder, beside the source rather than the build output.</summary>
    public static string AssetsDirectory { get; } = Path.GetFullPath(Path.Combine(AssemblyUtility.GetAssemblyDirectory()!, "../../../", "Assets"));
    public static string InputDirectory => Path.Combine(AssetsDirectory, "Input");
    public static string OutputDirectory => Path.Combine(AssetsDirectory, "Output");

    protected static string InputPath(string filename) => Path.Combine(InputDirectory, filename);

    protected static IMemoryFile ReadAsset(string filename) => File.ReadAllBytes(InputPath(filename)).ToMemoryFile();

    /// <summary>What an input PDF holds; not recorded.</summary>
    protected static PdfFacts InputFacts(string filename) => PdfFacts.Read(File.ReadAllBytes(InputPath(filename)));

    /// <summary>
    /// Saves a produced PDF under the running test's name and reads it with <see cref="PdfFacts"/>, recording the
    /// facts beside it. Asserts first that the file hands out its whole content without a rewind.
    /// </summary>
    /// <param name="suffix">Tells apart several files of one test</param>
    protected Task<PdfFacts> ReadPdf(IMemoryFile? output, string? suffix = null)
    {
        PdfTestHelper.AssertReadableWithoutRewind(output);
        return Record(output!, "pdf", suffix, PdfFacts.Read);
    }

    /// <summary>Saves a produced image under the running test's name and reads it with <see cref="ImageFacts"/>.</summary>
    protected Task<ImageFacts> ReadImage(IMemoryFile image, string? suffix = null)
        => Record(image, (ImageFacts.Sniff(image.GetBytes()!)?.ToString() ?? "bin").ToLowerInvariant().Replace("jpeg", "jpg"), suffix, ImageFacts.Read);

    private async Task<TFacts> Record<TFacts>(IMemoryFile output, string extension, string? suffix, Func<IMemoryFile, TFacts> read)
    {
        var path = TestOutputPath(extension, suffix);
        await File.WriteAllBytesAsync(path, output.GetBytes()!);
        _outputs.Add(path);
        var facts = read(output);
        var factsPath = $"{path}.facts.json";
        await File.WriteAllTextAsync(factsPath, JsonSerializer.Serialize(facts, FactsJson));
        _outputs.Add(factsPath);
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
