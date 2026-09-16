using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Word.Models;
using Regira.Utilities;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// Asset and output paths shared by every Word fixture: the backend fixtures through
/// <see cref="WordTestsBase"/>, and fixtures that exercise a helper rather than a backend directly.
/// </summary>
public abstract class WordAssetsTestsBase
{
    protected readonly string AssetsDir;
    protected readonly string InputDir;
    protected readonly string OutputDir;

    protected WordAssetsTestsBase(string outputFolderName)
    {
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        AssetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        InputDir = Path.Combine(AssetsDir, "Input");
        // Fixtures run in parallel (see Properties/AssemblyInfo.cs) and every backend writes the
        // same file names, so each one gets its own output folder.
        OutputDir = Path.Combine(AssetsDir, "Output", outputFolderName);
        Directory.CreateDirectory(OutputDir);
    }

    protected string InputPath(string filename) => Path.Combine(InputDir, filename);

    /// <summary>
    /// Resolves an output path, creating its folder and removing a result left by a previous run.
    /// </summary>
    protected string OutputPath(string filename)
    {
        var path = Path.Combine(OutputDir, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return path;
    }

    protected IBinaryFile ReadAsset(string filename) => File.ReadAllBytes(InputPath(filename)).ToBinaryFile();

    protected WordTemplateInput TemplateInput(string filename) => new() { Template = ReadAsset(filename) };
}
