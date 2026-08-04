namespace Regira.TreeList;

public class InvalidChildException<T> : Exception
{
    public TreeNode<T>? ParentNode { get; set; }
    public T Child { get; set; } = default!;
    internal InvalidChildException()
        : base("Child cannot be an ancestor of it's parent")
    {
    }
}