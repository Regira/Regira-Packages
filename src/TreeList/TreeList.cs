using Regira.TreeList.Abstractions;

namespace Regira.TreeList;

/// <summary>
/// Represents a hierarchical collection of tree nodes, where each node can have a value and a collection of child nodes.
/// Provides functionality for managing and interacting with the tree structure, including adding, removing, and validating nodes.
/// </summary>
/// <typeparam name="T">The type of the value stored in each tree node.</typeparam>
public class TreeList<T> : List<TreeNode<T>>
{
    public TreeOptions? Options { get; set; }

    public class TreeOptions
    {
        public bool EnableAutoCheck { get; set; } = true;
        public bool ThrowOnError { get; set; } = true;
    }

    public bool EnableAutoCheck { get; set; }
    public bool ThrowOnError { get; set; }
    public TreeNode<T>[] Roots { get; private set; } = [];


    public TreeList(TreeOptions? options = null)
    {
        Options ??= options ?? new TreeOptions();
        EnableAutoCheck = Options.EnableAutoCheck;
        ThrowOnError = Options.ThrowOnError;
    }
    public TreeList(int capacity, TreeOptions? options = null)
        : base(capacity)
    {
        Options = options ?? new TreeOptions();
        EnableAutoCheck = Options.EnableAutoCheck;
        ThrowOnError = Options.ThrowOnError;
    }
    public TreeList(IEnumerable<T> collection, TreeOptions? options = null)
    {
        Options = options ?? new TreeOptions();
        EnableAutoCheck = Options.EnableAutoCheck;
        ThrowOnError = Options.ThrowOnError;

        AddValues(collection);
    }
    public TreeList(IEnumerable<TreeNode<T>> collection, TreeOptions? options = null)
        : base(collection)
    {
        options ??= new TreeOptions();
        EnableAutoCheck = options.EnableAutoCheck;
        ThrowOnError = options.ThrowOnError;

        Roots = this.Where(node => node.Parent == null).ToArray();
    }


    public TreeNode<T>? AddValue(T value, TreeNode<T>? parent = null)
    {
        return AddValues([value], parent).FirstOrDefault();
    }
    public IEnumerable<TreeNode<T>> AddValues(IEnumerable<T> values, TreeNode<T>? parent = null)
    {
        if (parent == null)
        {
            var roots = values.Select(x => new TreeNode<T>(x, this)).ToArray();
            AddRange(roots);
            Roots = Roots.Concat(roots).ToArray();
            return roots;
        }

        return parent.AddChildren(values);
    }

    public void Fill(IEnumerable<T> values, Func<T, T> getParent)
    {
        Fill(values, value => new[] { getParent(value) }.Where(parent => parent != null));
    }
    /// <summary>
    /// Fill TreeList by defining each node's parents.
    /// Lesser performance.
    /// </summary>
    /// <param name="values">Complete collection of items</param>
    /// <param name="getParents">Selector of all parents for each item</param>
    public void Fill(IEnumerable<T> values, Func<T, IEnumerable<T>> getParents)
    {
        var list = values as IList<T> ?? values.ToList();
        var roots = list.Where(x => !getParents(x).Any());
        Fill(roots, node => list.Where(listValue => getParents(listValue).Contains(node.Value)));
    }
    /// <summary>
    /// Fill TreeList by defining each node's children.
    /// Best performance!
    /// </summary>
    /// <param name="rootValues">Only the root items</param>
    /// <param name="getChildren">Selector of all children for each node</param>
    public void Fill(IEnumerable<T> rootValues, Func<ITreeNode<T>, IEnumerable<T>> getChildren)
    {
        Clear();
        var rootList = rootValues as IList<T> ?? rootValues.ToList();
        foreach (var root in rootList)
        {
            var rootNode = AddValue(root);
            if (rootNode != null)
            {
                AddChildren(rootNode, getChildren);
            }
        }
    }
    private void AddChildren(TreeNode<T> node, Func<ITreeNode<T>, IEnumerable<T>> getChildren)
    {
        var children = getChildren(node);
        foreach (var child in children)
        {
            var childNode = node.AddChild(child);
            AddChildren(childNode, getChildren);
        }
    }

    public new void RemoveAt(int index)
    {
        if (index >= Count || index < 0)
        {
            throw new Exception("Invalid index");
        }

        var item = this[index];

        base.RemoveAt(index);

        if (item.Parent == null)
        {
            Roots = Roots.Where(r => !ReferenceEquals(r, item)).ToArray();
        }
        else
        {
            item.Parent.RemoveChild(item);
        }

        foreach (var child in item.Children)
        {
            Remove(child);
        }
    }
    public new bool Remove(TreeNode<T> item)
    {
        int index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
        }

        return index >= 0;
    }
    public int Remove(T value)
    {
        var items = FindAll(i => i.Value?.Equals(value) == true);
        return RemoveAll(items);
    }
    public int RemoveAll(IEnumerable<TreeNode<T>> items)
    {
        return items.Count(Remove);
    }
    public int RemoveAll(Func<TreeNode<T>, bool> predicate)
    {
        var items = this.Where(predicate).ToList();
        return items.Count(Remove);
    }

    public bool IsValidChild(TreeNode<T> parent, T child)
    {
        var ancestors = parent.GetAncestors();
        return ancestors.All(a => a.Value?.Equals(child) != true);
    }
}