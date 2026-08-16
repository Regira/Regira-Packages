using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Regira.GuideVerifier;

/// <summary>
/// Materializes a set of snippets into a throwaway compilation project on disk: one <c>.cs</c> file per
/// snippet plus a generated <c>.csproj</c> that ProjectReferences the manifest's src projects. Written to
/// a temp directory OUTSIDE the repo tree so it never inherits the repo's Directory.Build.props (which
/// would drag in packaging targets).
/// </summary>
public static class GeneratedProject
{
    // Usings prepended to every snippet so ubiquitous BCL symbols resolve without each snippet restating
    // them; the manifest group's `usings` add the family-specific namespaces on top. Declaration snippets
    // keep their own usings (Roslyn preserves them); these are added too, harmless when unused (CS8019 is
    // not an error).
    private static readonly string[] BaseUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Threading.Tasks",
    ];

    /// <summary>Writes the project and its snippet files; returns the generated .csproj path.</summary>
    public static string Write(
        string dir,
        IReadOnlyList<Snippet> snippets,
        IReadOnlyList<string> projectReferences,
        IReadOnlyList<string> groupUsings,
        IReadOnlyList<string> frameworkReferences,
        IReadOnlyDictionary<string, string>? packages = null,
        string? sharedNamespace = null)
    {
        Directory.CreateDirectory(dir);

        var usings = BaseUsings.Concat(groupUsings).Distinct().ToList();
        foreach (var snippet in snippets)
            File.WriteAllText(Path.Combine(dir, $"{snippet.Id}.cs"), Emit(snippet, usings, sharedNamespace));

        var csprojPath = Path.Combine(dir, "GuideSnippets.csproj");
        File.WriteAllText(csprojPath, Csproj(projectReferences, frameworkReferences, packages));
        return csprojPath;
    }

    /// <param name="sharedNamespace">
    /// Set for a **narrative** guide, where later blocks use types the earlier ones declare (a quickstart:
    /// §2 defines the entities, §3 registers them, §4 writes their controllers). Every snippet in the group
    /// then lands in this one namespace instead of its own, so the blocks compile as the walkthrough reads.
    /// Leave null for reference guides, where per-snippet isolation is what keeps two files' `Product` apart.
    /// </param>
    private static string Emit(Snippet snippet, IReadOnlyList<string> allUsings, string? sharedNamespace = null)
    {
        var usings = string.Join("\n", allUsings.Select(u => $"using {u};"));
        var provenance = $"// {snippet.Location} (line {snippet.FenceLine} of {snippet.RelativeFile})";
        var sb = new StringBuilder();
        sb.Append(provenance).Append('\n');
        sb.Append("#pragma warning disable\n");
        sb.Append(usings).Append("\n\n");

        if (snippet.Kind == SnippetKind.Declarations)
        {
            // Declarations live at namespace scope. Give each snippet its own namespace to avoid type-name
            // collisions between guides. The snippet may carry its own usings/namespace declarations.
            sb.Append("namespace ").Append(sharedNamespace ?? $"GuideSnippets.{snippet.Id}").Append('\n');
            sb.Append("{\n");
            sb.Append(Indent(snippet.Code, "    ")).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        // Statements/expressions: wrap in an async method body inside a unique class. `sp`/`scope` mirror
        // the service-provider variables the guides use to resolve services, and `args` the parameter a
        // top-level `Program.cs` receives. A statement block may still open with its own usings (a host
        // snippet does) — those belong at file scope, above the namespace.
        // Roslyn, not a line prefix: `using var stream = File.OpenRead(…)` and `using (var scope = …)` both
        // start with "using " and are STATEMENTS. Only a real UsingDirectiveSyntax may be hoisted.
        var root = CSharpSyntaxTree.ParseText(snippet.Code).GetCompilationUnitRoot();
        var body = snippet.Code;
        if (root.Usings.Count > 0)
        {
            foreach (var directive in root.Usings)
                sb.Append(directive.ToString().Trim()).Append('\n');
            body = snippet.Code[root.Usings.Last().FullSpan.End..];
        }

        sb.Append("namespace ").Append(sharedNamespace ?? "GuideSnippets").Append("\n{\n");
        sb.Append("    internal static class ").Append(snippet.Id).Append('\n');
        sb.Append("    {\n");
        // Ambient as FIELDS, not parameters: a snippet is free to declare its own `scope` (a host block
        // writes `using (var scope = app.Services.CreateScope())`), and a local may legally shadow a field
        // where shadowing a parameter is CS0136.
        sb.Append("        private static System.IServiceProvider sp = null!;\n");
        sb.Append("        private static System.IServiceProvider scope = null!;\n");
        sb.Append("        private static string[] args = [];\n");
        sb.Append("        internal static async System.Threading.Tasks.Task Run()\n");
        sb.Append("        {\n");
        sb.Append("            await System.Threading.Tasks.Task.CompletedTask;\n");
        sb.Append(Indent(body, "            ")).Append('\n');
        sb.Append("        }\n");
        sb.Append("    }\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string Indent(string code, string indent) =>
        string.Join("\n", code.Replace("\r\n", "\n").Split('\n').Select(l => l.Length == 0 ? l : indent + l));

    private static string Csproj(IReadOnlyList<string> projectReferences, IReadOnlyList<string> frameworkReferences,
        IReadOnlyDictionary<string, string>? packages = null)
    {
        var refs = new StringBuilder();
        foreach (var f in frameworkReferences)
            refs.AppendLine($"    <FrameworkReference Include=\"{f}\" />");
        foreach (var (id, version) in packages ?? new Dictionary<string, string>())
            refs.AppendLine($"    <PackageReference Include=\"{id}\" Version=\"{version}\" />");
        foreach (var p in projectReferences)
            refs.AppendLine($"    <ProjectReference Include=\"{p}\" />");

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>disable</Nullable>
                <ImplicitUsings>disable</ImplicitUsings>
                <!-- Compile-only: we never run this assembly, we only care whether the snippets bind. -->
                <OutputType>Library</OutputType>
                <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <NoWarn>$(NoWarn);CS1701;CS1702</NoWarn>
                <!-- This project sits outside the repo tree; make sure no stray Directory.Build.* leaks in. -->
                <ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>
                <ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets>
              </PropertyGroup>

              <ItemGroup>
            {refs.ToString().TrimEnd()}
              </ItemGroup>

            </Project>
            """;
    }
}
