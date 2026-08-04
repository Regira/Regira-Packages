using System.Text.Json;
using Regira.GuideVerifier;

// Compile-the-guides verifier. Scans the manifest's ai/ folders for fenced ```csharp blocks (skipping
// ```csharp no-compile), classifies each as a declaration or a statement snippet, emits a throwaway
// compilation project that ProjectReferences the manifest's src projects, runs `dotnet build`, and
// reports every failure as `file.md § heading`. Exits non-zero on any compile failure.
//
//   dotnet run --project tools/verify-guides [-- repoRoot]
//
// Best-effort: many guide snippets are partial fragments and must be marked ```csharp no-compile (see
// tools/verify-guides/README.md). The value is that fully-formed snippets are now compiler-checked.

// First positional (non-flag) arg is the repo root; ignore stray flags like --nologo so a mistaken
// `dotnet run ... --nologo` doesn't get treated as a path.
var repoRoot = args.FirstOrDefault(a => !a.StartsWith('-')) ?? FindRepoRoot();
Console.WriteLine($"Guide verifier — repo root: {repoRoot}");

var manifestPath = Path.Combine(AppContext.BaseDirectory, "projects.json");
if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"projects.json not found next to the verifier ({manifestPath}).");
    return 1;
}

var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
}) ?? throw new InvalidOperationException("Failed to parse projects.json.");

var projectRefs = manifest.Projects.Select(p => Path.GetFullPath(Path.Combine(repoRoot, p))).ToList();
var missingRefs = projectRefs.Where(p => !File.Exists(p)).ToList();
if (missingRefs.Count > 0)
{
    Console.Error.WriteLine("Referenced project(s) not found:");
    foreach (var m in missingRefs) Console.Error.WriteLine($"  - {m}");
    return 1;
}

// ── Collect snippets ──────────────────────────────────────────────────────────
var snippets = new List<Snippet>();
var counter = 0;
foreach (var guideDir in manifest.GuideDirs)
{
    var dir = Path.Combine(repoRoot, guideDir);
    if (!Directory.Exists(dir)) continue;

    foreach (var file in Directory.GetFiles(dir, "*.md").OrderBy(f => f, StringComparer.Ordinal))
    {
        var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        foreach (var raw in SnippetExtractor.Extract(relative, File.ReadAllText(file)))
        {
            var kind = SnippetClassifier.Classify(raw.Code);
            var id = $"snippet_{++counter:D3}_{Sanitize(Path.GetFileNameWithoutExtension(file))}";
            snippets.Add(raw with { Kind = kind, Id = id });
        }
    }
}

Console.WriteLine($"Found {snippets.Count} compilable ```csharp block(s) across {manifest.GuideDirs.Count} guide folder(s).");
if (snippets.Count == 0)
{
    Console.WriteLine("Nothing to verify. (All csharp blocks are marked no-compile, or there are none.)");
    return 0;
}

// ── Generate + build ──────────────────────────────────────────────────────────
var work = Path.Combine(Path.GetTempPath(), "regira-guide-verify-" + Guid.NewGuid().ToString("N"));
try
{
    var csproj = GeneratedProject.Write(work, snippets, projectRefs);
    Console.WriteLine($"Generated snippet project: {csproj}");
    Console.WriteLine("Building…");

    var outcome = BuildRunner.Build(csproj);

    if (outcome.Errors.Count == 0 && outcome.ExitCode == 0)
    {
        Console.WriteLine($"\nOK — all {snippets.Count} snippet(s) compiled.");
        return 0;
    }

    // Map each error back to its snippet's location.
    var byId = snippets.ToDictionary(s => s.Id);
    string Label(string id) => byId.TryGetValue(id, out var s)
        ? $"{s.Location}  [{s.RelativeFile}:{s.FenceLine}]"
        : id;
    var grouped = outcome.Errors
        .GroupBy(e => Label(e.SnippetId))
        .OrderBy(g => g.Key, StringComparer.Ordinal);

    Console.Error.WriteLine($"\nFAILED — {outcome.Errors.Count} compile error(s):\n");
    foreach (var group in grouped)
    {
        Console.Error.WriteLine($"  {group.Key}");
        foreach (var e in group)
            Console.Error.WriteLine($"    {e.Code}: {e.Message}");
    }

    if (outcome.Errors.Count == 0)
    {
        // Non-zero exit with no parsed CS errors — surface the raw output so the failure isn't silent.
        Console.Error.WriteLine("\nBuild failed but no CS diagnostics were parsed. Raw output:\n");
        Console.Error.WriteLine(outcome.RawOutput);
    }

    Console.Error.WriteLine(
        "\nEach fully-formed snippet must compile. Fix the guide, or if the block is a genuine partial " +
        "fragment, mark it ```csharp no-compile (see tools/verify-guides/README.md).");
    return 1;
}
finally
{
    try { Directory.Delete(work, recursive: true); } catch { /* best-effort temp cleanup */ }
}

static string Sanitize(string name)
{
    var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
    return new string(chars);
}

static string FindRepoRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "Regira-Packages.slnx")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    throw new InvalidOperationException("Could not locate repo root (Regira-Packages.slnx). Pass it as an argument.");
}

internal record Manifest(List<string> Projects, List<string> GuideDirs);
