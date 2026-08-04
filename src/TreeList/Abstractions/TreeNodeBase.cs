namespace Regira.TreeList.Abstractions;

/// <summary>
/// Serves as the base class for tree nodes, providing common functionality and structure 
/// for nodes in a tree hierarchy.
/// </summary>
/// <typeparam name="T">
/// The type of the value stored in the tree node.
/// </typeparam>
public abstract class TreeNodeBase<T> : ITreeNode<T>
{
    public T Value { get; protected set; } = default!;
    public int Level { get; protected set; }
    public TreeNode<T>? Root { get; protected set; }
    public TreeNode<T>? Parent { get; protected set; }
    public abstract ICollection<TreeNode<T>> Children { get; }
}