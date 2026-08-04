using System.Text;

namespace Regira.GuideVerifier;

/// <summary>
/// Materializes a set of snippets into a throwaway compilation project on disk: one <c>.cs</c> file per
/// snippet plus a generated <c>.csproj</c> that ProjectReferences the manifest's src projects. Written to
/// a temp directory OUTSIDE the repo tree so it never inherits the repo's Directory.Build.props (which
/// would drag in packaging targets).
/// </summary>
public static class GeneratedProject
{
    // Usings prepended to every STATEMENT snippet so the common Regira Entities / EF Core / DI symbols the
    // guides reference resolve without each snippet restating them. Declaration snippets keep their own
    // usings (Roslyn preserves them); these are added too, harmless when unused (CS8019 is not an error).
    private static readonly string[] CommonUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Threading.Tasks",
        "System.ComponentModel.DataAnnotations",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions.DependencyInjection",
        "Regira.Entities.Models",
        "Regira.Entities.Models.Abstractions",
        "Regira.Entities.Services.Abstractions",
        "Regira.Entities.Extensions",
        "Regira.Entities.Attachments.Models",
        "Regira.Entities.Attachments.Abstractions",
        "Regira.Entities.DependencyInjection.Extensions",
        "Regira.Entities.DependencyInjection.Attachments",
    ];

    /// <summary>Writes the project and its snippet files; returns the generated .csproj path.</summary>
    public static string Write(string dir, IReadOnlyList<Snippet> snippets, IReadOnlyList<string> projectReferences)
    {
        Directory.CreateDirectory(dir);

        foreach (var snippet in snippets)
            File.WriteAllText(Path.Combine(dir, $"{snippet.Id}.cs"), Emit(snippet));

        var csprojPath = Path.Combine(dir, "GuideSnippets.csproj");
        File.WriteAllText(csprojPath, Csproj(projectReferences));
        return csprojPath;
    }

    private static string Emit(Snippet snippet)
    {
        var usings = string.Join("\n", CommonUsings.Select(u => $"using {u};"));
        var provenance = $"// {snippet.Location} (line {snippet.FenceLine} of {snippet.RelativeFile})";
        var sb = new StringBuilder();
        sb.Append(provenance).Append('\n');
        sb.Append("#pragma warning disable\n");
        sb.Append(usings).Append("\n\n");

        if (snippet.Kind == SnippetKind.Declarations)
        {
            // Declarations live at namespace scope. Give each snippet its own namespace to avoid type-name
            // collisions between guides. The snippet may carry its own usings/namespace declarations.
            sb.Append("namespace GuideSnippets.").Append(snippet.Id).Append('\n');
            sb.Append("{\n");
            sb.Append(Indent(snippet.Code, "    ")).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        // Statements/expressions: wrap in an async method body inside a unique class. `sp`/`scope` mirror
        // the service-provider variables the guides use to resolve services.
        sb.Append("namespace GuideSnippets\n{\n");
        sb.Append("    internal static class ").Append(snippet.Id).Append('\n');
        sb.Append("    {\n");
        sb.Append("        internal static async System.Threading.Tasks.Task Run(System.IServiceProvider sp, System.IServiceProvider scope)\n");
        sb.Append("        {\n");
        sb.Append("            await System.Threading.Tasks.Task.CompletedTask;\n");
        sb.Append(Indent(snippet.Code, "            ")).Append('\n');
        sb.Append("        }\n");
        sb.Append("    }\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string Indent(string code, string indent) =>
        string.Join("\n", code.Replace("\r\n", "\n").Split('\n').Select(l => l.Length == 0 ? l : indent + l));

    private static string Csproj(IReadOnlyList<string> projectReferences)
    {
        var refs = new StringBuilder();
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
