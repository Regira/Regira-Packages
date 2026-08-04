using System.Collections.ObjectModel;

namespace Regira.TreeList;

/// <summary>
/// Represents a read-only view of a hierarchical tree structure, providing access to the ordered collection of node values.
/// </summary>
/// <typeparam name="T">The type of the value stored in each tree node.</typeparam>
/// <param name="tree">The underlying <see cref="TreeList{T}"/> instance that defines the tree structure.</param>
public class TreeView<T>(TreeList<T> tree)
    : ReadOnlyCollection<T>(tree.OrderByHierarchy().Select(n => n.Value).ToList())
{
    public TreeList<T> Tree { get; } = tree;
}