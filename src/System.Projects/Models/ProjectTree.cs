using Regira.IO.Storage.FileSystem;
using Regira.TreeList;
using Regira.Utilities;

namespace Regira.System.Projects.Models;

public class ProjectTree : TreeList<Project>
{
    public IList<Project> Values => this.Select(n => n.Value).ToArray();

    public static ProjectTree Load(IEnumerable<Project> items)
    {
        var collection = items.AsList();
        var tree = new ProjectTree();

        // Children are indexed by the resolved absolute path of the dependency. A ProjectReference is
        // relative to the project declaring it, so matching on the relative suffix alone made every copy of
        // a project below the scan root -- a git worktree, an unpacked archive -- look like one and the same
        // project. That cross-linked the copies into a single graph and multiplied the paths walked below.
        var childrenByDependency = new Dictionary<string, List<Project>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in collection)
        {
            var directory = Path.GetDirectoryName(project.ProjectFile);
            var dependencies = project.Dependencies
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => FullPath(FileNameUtility.Combine(directory, d)))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in dependencies)
            {
                if (!childrenByDependency.TryGetValue(dependency, out var children))
                {
                    childrenByDependency[dependency] = children = [];
                }
                children.Add(project);
            }
        }

        void Add(TreeNode<Project> node)
        {
            if (!childrenByDependency.TryGetValue(FullPath(node.Value.ProjectFile), out var children))
            {
                return;
            }
            foreach (var childProject in children)
            {
                var childNode = node.AddChild(childProject);
                if (childNode != null)
                {
                    Add(childNode);
                }
            }
        }

        foreach (var root in collection.Where(x => !x.Dependencies.Any()))
        {
            Add(tree.AddValue(root)!);
        }

        return tree;
    }

    /// <summary>
    /// Resolves a path to its canonical absolute form, so the same project file always yields the same key
    /// regardless of how the reference to it was written.
    /// </summary>
    private static string FullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = FileNameUtility.ConvertForwardSlashes(path);
        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            return normalized;
        }
    }
}
