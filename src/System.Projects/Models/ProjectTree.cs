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
        var roots = collection.Where(x => !x.Dependencies.Any()).ToArray();

        void Add(TreeNode<Project> node)
        {
            var children = collection.Where(c => c.Dependencies.Any(d => node.Value.ProjectFile.EndsWith(d.TrimStart('.'), StringComparison.InvariantCultureIgnoreCase)));
            foreach (var childProject in children)
            {
                var childNode = node.AddChild(childProject);
                Add(childNode);
            }
        }

        foreach (var root in roots)
        {
            Add(tree.AddValue(root)!);
        }

        return tree;
    }
}
