namespace Regira.System.Projects.Models;

public class Project
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string ProjectFile { get; set; } = null!;
    public string? RootNamespace { get; set; }
    public string? AssemblyName { get; set; }
    public ICollection<string>? TargetFrameworks { get; set; }
    public ICollection<string>? Authors { get; set; }
    public Version Version { get; set; } = new("1.0.0");
    public bool? IsClassLibrary { get; set; }

    public ICollection<string> Dependencies { get; set; } = Array.Empty<string>();
}