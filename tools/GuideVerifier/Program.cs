using System.Text.Json;
using Regira.GuideVerifier;

// Compile-the-guides verifier. The manifest defines snippet GROUPS; each group pairs a set of guide
// files/folders with the src projects their snippets compile against. Per group: scan for fenced
// ```csharp blocks (skipping ```csharp no-compile), classify each as a declaration or a statement
// snippet, emit a throwaway compilation project that ProjectReferences the group's src projects, run
// `dotnet build`, and report every failure as `file.md § heading`. Exits non-zero when any group fails.
//
//   dotnet run --project tools/GuideVerifier [-- repoRoot] [--group name[,name…]]
//
// Best-effort: many guide snippets are partial fragments and must be marked ```csharp no-compile (see
// tools/GuideVerifier/README.md). The value is that fully-formed snippets are now compiler-checked.

// First positional (non-flag) arg is the repo root; ignore stray flags like --nologo so a mistaken
// `dotnet run ... --nologo` doesn't get treated as a path. `--group` filters to the named group(s).
var repoRoot = args.FirstOrDefault(a => !a.StartsWith('-')) ?? FindRepoRoot();
var groupFilter = ParseGroupFilter(args);
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

// Legacy single-group manifests (top-level projects/guideDirs) still work, as the one group "default".
var groups = manifest.Groups is { Count: > 0 }
    ? manifest.Groups
    : [new GroupManifest("default", manifest.Projects ?? [], manifest.GuideDirs, null, null, null)];

if (groupFilter is not null)
{
    var unknown = groupFilter.Where(f => !groups.Any(g => g.Name.Equals(f, StringComparison.OrdinalIgnoreCase))).ToList();
    if (unknown.Count > 0)
    {
        Console.Error.WriteLine($"Unknown group(s): {string.Join(", ", unknown)}. Available: {string.Join(", ", groups.Select(g => g.Name))}");
        return 1;
    }
    groups = groups.Where(g => groupFilter.Contains(g.Name, StringComparer.OrdinalIgnoreCase)).ToList();
}

var failedGroups = new List<string>();
var totalSnippets = 0;
foreach (var group in groups)
{
    Console.WriteLine($"\n── group: {group.Name} ──");

    var projectRefs = group.Projects.Select(p => Path.GetFullPath(Path.Combine(repoRoot, p))).ToList();
    var missingRefs = projectRefs.Where(p => !File.Exists(p)).ToList();
    if (missingRefs.Count > 0)
    {
        Console.Error.WriteLine("Referenced project(s) not found:");
        foreach (var m in missingRefs) Console.Error.WriteLine($"  - {m}");
        failedGroups.Add(group.Name);
        continue;
    }

    // ── Collect snippets ──────────────────────────────────────────────────────
    var guideFiles = new List<string>();
    foreach (var guideDir in group.GuideDirs ?? [])
    {
        var dir = Path.Combine(repoRoot, guideDir);
        if (Directory.Exists(dir))
            guideFiles.AddRange(Directory.GetFiles(dir, "*.md").OrderBy(f => f, StringComparer.Ordinal));
    }
    foreach (var guideFile in group.GuideFiles ?? [])
    {
        var file = Path.Combine(repoRoot, guideFile);
        if (File.Exists(file)) guideFiles.Add(file);
        else Console.Error.WriteLine($"  (guide file not found, skipping: {guideFile})");
    }

    var snippets = new List<Snippet>();
    var counter = 0;
    foreach (var file in guideFiles)
    {
        var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        foreach (var raw in SnippetExtractor.Extract(relative, File.ReadAllText(file)))
        {
            var kind = SnippetClassifier.Classify(raw.Code);
            var id = $"snippet_{++counter:D3}_{Sanitize(Path.GetFileNameWithoutExtension(file))}";
            snippets.Add(raw with { Kind = kind, Id = id });
        }
    }

    Console.WriteLine($"Found {snippets.Count} compilable ```csharp block(s) across {guideFiles.Count} guide file(s).");
    totalSnippets += snippets.Count;
    if (snippets.Count == 0)
    {
        Console.WriteLine("Nothing to verify. (All csharp blocks are marked no-compile, or there are none.)");
        continue;
    }

    // ── Generate + build ──────────────────────────────────────────────────────
    var work = Path.Combine(Path.GetTempPath(), $"regira-guide-verify-{Sanitize(group.Name)}-" + Guid.NewGuid().ToString("N"));
    try
    {
        var csproj = GeneratedProject.Write(work, snippets, projectRefs, group.Usings ?? [], group.FrameworkReferences ?? []);
        Console.WriteLine($"Generated snippet project: {csproj}");
        Console.WriteLine("Building…");

        var outcome = BuildRunner.Build(csproj);

        if (outcome.Errors.Count == 0 && outcome.ExitCode == 0)
        {
            Console.WriteLine($"OK — all {snippets.Count} snippet(s) compiled.");
            continue;
        }

        failedGroups.Add(group.Name);

        // Map each error back to its snippet's location.
        var byId = snippets.ToDictionary(s => s.Id);
        string Label(string id) => byId.TryGetValue(id, out var s)
            ? $"{s.Location}  [{s.RelativeFile}:{s.FenceLine}]"
            : id;
        var grouped = outcome.Errors
            .GroupBy(e => Label(e.SnippetId))
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        Console.Error.WriteLine($"\nFAILED ({group.Name}) — {outcome.Errors.Count} compile error(s):\n");
        foreach (var errorGroup in grouped)
        {
            Console.Error.WriteLine($"  {errorGroup.Key}");
            foreach (var e in errorGroup)
                Console.Error.WriteLine($"    {e.Code}: {e.Message}");
        }

        if (outcome.Errors.Count == 0)
        {
            // Non-zero exit with no parsed CS errors — surface the raw output so the failure isn't silent.
            Console.Error.WriteLine("\nBuild failed but no CS diagnostics were parsed. Raw output:\n");
            Console.Error.WriteLine(outcome.RawOutput);
        }
    }
    finally
    {
        try { Directory.Delete(work, recursive: true); } catch { /* best-effort temp cleanup */ }
    }
}

if (failedGroups.Count == 0)
{
    Console.WriteLine($"\nOK — {groups.Count} group(s), {totalSnippets} snippet(s) compiled.");
    return 0;
}

Console.Error.WriteLine(
    $"\nFAILED group(s): {string.Join(", ", failedGroups)}.\n" +
    "Each fully-formed snippet must compile. Fix the guide, or if the block is a genuine partial " +
    "fragment, mark it ```csharp no-compile (see tools/GuideVerifier/README.md).");
return 1;

static string Sanitize(string name)
{
    var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
    return new string(chars);
}

static List<string>? ParseGroupFilter(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--group" && i + 1 < args.Length)
            return args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (args[i].StartsWith("--group=", StringComparison.Ordinal))
            return args[i]["--group=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
    return null;
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

internal record GroupManifest(
    string Name,
    List<string> Projects,
    List<string>? GuideDirs,
    List<string>? GuideFiles,
    List<string>? Usings,
    List<string>? FrameworkReferences);

internal record Manifest(List<GroupManifest>? Groups, List<string>? Projects, List<string>? GuideDirs);
