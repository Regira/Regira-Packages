using Regira.TreeList.Abstractions;

namespace Regira.TreeList;

/// <summary>
/// Represents a node in a tree structure, where each node can have a value, a parent, and multiple children.
/// </summary>
/// <typeparam name="T">
/// The type of the value stored in the tree node.
/// </typeparam>
public class TreeNode<T> : TreeNodeBase<T>
{
    private TreeNode<T>[]? _children;
    public override ICollection<TreeNode<T>> Children => _children ??= [];
    public TreeList<T> Tree { get; }

    internal TreeNode(T value, TreeList<T> tree)
    {
        Tree = tree;
        Value = value;
        Level = 0;
    }
    private TreeNode(T value, TreeNode<T> parent)
    {
        Tree = parent.Tree;
        Value = value;
        Parent = parent;
        Root = parent.Root ?? parent;
        Level = parent.Level + 1;
    }

    public TreeNode<T> AddChild(T value)
    {
        return AddChildren([value]).First();
    }
    public IEnumerable<TreeNode<T>> AddChildren(IEnumerable<T> values)
    {
        var childNodes = values
            .Where(v =>
            {
                if (Tree.EnableAutoCheck && !Tree.IsValidChild(this, v))
                {
                    if (Tree.ThrowOnError)
                    {
                        throw new InvalidChildException<T>
                        {
                            ParentNode = this,
                            Child = v
                        };
                    }

                    return false;
                }

                return true;
            })
            .Select(v => new TreeNode<T>(v, this))
            .ToArray();

        if (childNodes.Any())
        {
            _children = Children.Concat(childNodes).ToArray();
            Tree.AddRange(childNodes);
        }

        return childNodes;
    }
    public bool RemoveChild(TreeNode<T> child)
    {
        bool found = false;
        if (_children != null)
        {
            var children = _children.ToList();
            found = children.Remove(child);
            _children = children.ToArray();
        }

        if (found)
        {
            Tree.Remove(child);
        }
        return found;
    }

    public override string? ToString()
    {
        return Value?.ToString();
    }
}