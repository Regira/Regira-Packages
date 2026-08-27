namespace Regira.TreeList;

/// <summary>
/// Thrown when a value cannot be placed in the tree: it is an ancestor of the parent it is added to
/// (a circular reference), or it is unreachable from any root when filling from a parents-selector.
/// </summary>
/// <typeparam name="T">The type of the value stored in the tree.</typeparam>
public class InvalidChildException<T> : Exception
{
    /// <summary>
    /// The parent the child was added to, when the exception comes from an add-path. <c>null</c> for unreachable values.
    /// </summary>
    public TreeNode<T>? ParentNode { get; set; }
    /// <summary>
    /// The offending value.
    /// </summary>
    public T Child { get; set; } = default!;
    internal InvalidChildException()
        : base("Child cannot be an ancestor of its parent")
    {
    }
    internal InvalidChildException(string message)
        : base(message)
    {
    }
}
