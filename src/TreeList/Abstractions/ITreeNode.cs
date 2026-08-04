namespace Regira.TreeList.Abstractions;

/// <summary>
/// Defines the structure and behavior of a tree node, which represents an element in a hierarchical tree structure.
/// </summary>
/// <typeparam name="T">
/// The type of the value stored in the tree node.
/// </typeparam>
public interface ITreeNode<T>
{
    T Value { get; }
    int Level { get; }
    TreeNode<T>? Root { get; }
    TreeNode<T>? Parent { get; }
    ICollection<TreeNode<T>> Children { get; }
}