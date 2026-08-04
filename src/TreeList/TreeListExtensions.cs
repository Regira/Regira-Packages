using Regira.TreeList.Abstractions;

namespace Regira.TreeList;

public static class TreeListExtensions
{
    /// <summary>
    /// Converts a collection of <see cref="TreeNode{T}"/> objects into a <see cref="TreeList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in the tree nodes.</typeparam>
    /// <param name="collection">The collection of <see cref="TreeNode{T}"/> objects to convert.</param>
    /// <returns>A <see cref="TreeList{T}"/> containing the nodes from the specified collection.</returns>
    public static TreeList<T> ToTreeList<T>(this IEnumerable<TreeNode<T>> collection) 
        => new(collection);
    /// <summary>
    /// Converts the specified collection of items into a <see cref="TreeList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="collection">The collection of items to convert into a tree structure.</param>
    /// <returns>A <see cref="TreeList{T}"/> containing the elements of the input collection.</returns>
    public static TreeList<T> ToTreeList<T>(this IEnumerable<T> collection)
    {
        var items = collection as IList<T> ?? collection.ToList();
        var tree = new TreeList<T>(items.Count);
        foreach (var item in items)
        {
            tree.AddValue(item);
        }
        return tree;
    }

    /// <summary>
    /// Converts a flat collection of items into a hierarchical <see cref="TreeList{T}"/> structure.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="collection">The flat collection of items to be converted into a tree structure.</param>
    /// <param name="getParent">
    /// A function that determines the parent of each item in the collection. 
    /// It should return the parent item for a given item or <c>null</c> if the item is a root.
    /// </param>
    /// <param name="options">Optional configuration for the resulting <see cref="TreeList{T}"/>.</param>
    /// <returns>A <see cref="TreeList{T}"/> representing the hierarchical structure of the input collection.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="collection"/> or <paramref name="getParent"/> is <c>null</c>.
    /// </exception>
    public static TreeList<T> ToTreeList<T>(this IEnumerable<T> collection, Func<T, T> getParent, TreeList<T>.TreeOptions? options = null)
        => ToTreeList(collection as IList<T> ?? collection.ToList(), i => new[] { getParent(i) }.Where(x => x != null), options);
    /// <summary>
    /// Converts a collection of items into a <see cref="TreeList{T}"/> structure, 
    /// using the specified function to determine the parent-child relationships.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="collection">The collection of items to be converted into a tree structure.</param>
    /// <param name="fkSelector">
    /// A function that takes an item and returns its parent(s) as an <see cref="IEnumerable{T}"/>.
    /// This function is used to establish the hierarchy within the tree.
    /// </param>
    /// <param name="options">
    /// Optional configuration for the tree, specified as an instance of <see cref="TreeList{T}.TreeOptions"/>.
    /// </param>
    /// <returns>
    /// A <see cref="TreeList{T}"/> representing the hierarchical structure of the input collection.
    /// </returns>
    public static TreeList<T> ToTreeList<T>(this IEnumerable<T> collection, Func<T, IEnumerable<T>> fkSelector, TreeList<T>.TreeOptions? options = null)
    {
        var items = collection as IList<T> ?? collection.ToList();
        var tree = new TreeList<T>(items.Count, options);
        tree.Fill(items, fkSelector);
        return tree;
    }
    /// <summary>
    /// Converts a collection of items into a <see cref="TreeList{T}"/> structure using the specified roots and child selector.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="collection">The collection of items to be converted into a tree structure.</param>
    /// <param name="roots">The root elements of the tree.</param>
    /// <param name="getChildren">
    /// A function that retrieves the child elements for a given tree node. 
    /// The function takes an <see cref="ITreeNode{T}"/> as input and returns an <see cref="IEnumerable{T}"/> of child elements.
    /// </param>
    /// <param name="options">Optional tree configuration options of type <see cref="TreeList{T}.TreeOptions"/>.</param>
    /// <returns>A <see cref="TreeList{T}"/> representing the hierarchical structure of the input collection.</returns>
    public static TreeList<T> ToTreeList<T>(this IEnumerable<T> collection, IEnumerable<T> roots, Func<ITreeNode<T>, IEnumerable<T>> getChildren, TreeList<T>.TreeOptions? options = null)
    {
        var items = collection as IList<T> ?? collection.ToList();
        var tree = new TreeList<T>(items.Count, options);
        tree.Fill(roots, getChildren);
        return tree;
    }

    /// <summary>
    /// Converts the specified <see cref="TreeList{T}"/> into a <see cref="TreeView{T}"/>, 
    /// providing a read-only view of the hierarchical tree structure.
    /// </summary>
    /// <typeparam name="T">The type of the value stored in each tree node.</typeparam>
    /// <param name="tree">The <see cref="TreeList{T}"/> instance to convert.</param>
    /// <returns>A <see cref="TreeView{T}"/> representing a read-only view of the tree structure.</returns>
    public static TreeView<T> ToTreeView<T>(this TreeList<T> tree) 
        => new(tree);

    /// <summary>
    /// Orders the nodes in the collection based on their hierarchical structure.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of <see cref="TreeNode{T}"/> to be ordered.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="TreeNode{T}"/> ordered by their hierarchy.
    /// </returns>
    public static IEnumerable<TreeNode<T>> OrderByHierarchy<T>(this IEnumerable<TreeNode<T>> collection) 
        => OrderByHierarchy<T, T>(collection);
    /// <summary>
    /// Orders the elements in the specified collection of <see cref="TreeNode{T}"/> objects 
    /// based on their hierarchy, optionally using a specified key selector for ordering.
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the tree nodes.</typeparam>
    /// <typeparam name="TKey">The type of the key used for ordering.</typeparam>
    /// <param name="collection">The collection of <see cref="TreeNode{T}"/> objects to be ordered.</param>
    /// <param name="keySelector">
    /// An optional function to extract a key from a <see cref="TreeNode{T}"/> for ordering. 
    /// If <c>null</c>, the nodes are ordered based on their natural hierarchy.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="TreeNode{T}"/> objects ordered by hierarchy.
    /// </returns>
    public static IEnumerable<TreeNode<T>> OrderByHierarchy<T, TKey>(this IEnumerable<TreeNode<T>> collection, Func<TreeNode<T>, TKey>? keySelector = null)
    {
        IEnumerable<TreeNode<T>> GetOrderedCollection(IEnumerable<TreeNode<T>> items)
        {
            if (keySelector != null)
            {
                return items.OrderBy(keySelector);
            }

            return items;
        }

        foreach (var root in GetOrderedCollection(collection.GetRoots()))
        {
            yield return root;
            foreach (var child in GetOrderedCollection(root.GetOffspring()))
            {
                yield return child;
            }
        }
    }

    //TreeNode extensions
    /// <summary>
    /// Retrieves the root node of the tree to which the specified <see cref="TreeNode{T}"/> belongs.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree node.
    /// </typeparam>
    /// <param name="item">
    /// The <see cref="TreeNode{T}"/> instance for which to find the root node.
    /// </param>
    /// <returns>
    /// The root <see cref="TreeNode{T}"/> of the tree. If the specified node is already the root, it is returned.
    /// </returns>
    public static TreeNode<T> GetRoot<T>(this TreeNode<T> item) 
        => item.GetAncestors().FirstOrDefault(a => a.Parent == null) ?? item;
    /// <summary>
    /// Retrieves all ancestor nodes of the specified <see cref="TreeNode{T}"/> in the hierarchy.
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the tree nodes.</typeparam>
    /// <param name="item">The <see cref="TreeNode{T}"/> for which to retrieve the ancestors.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing all ancestor nodes of the specified node, ordered from the closest parent to the root.</returns>
    public static IEnumerable<TreeNode<T>> GetAncestors<T>(this TreeNode<T> item)
    {
        IEnumerable<TreeNode<T>> InnerGetAncestors(TreeNode<T> node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                yield return parent;
                parent = parent.Parent;
            }
        }

        return InnerGetAncestors(item)
            .Reverse();
    }
    /// <summary>
    /// Retrieves the immediate child nodes of the specified tree node.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree node.
    /// </typeparam>
    /// <param name="item">
    /// The tree node whose child nodes are to be retrieved.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the child nodes of the specified tree node.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetChildren<T>(this TreeNode<T> item) 
        => item.Children;
    /// <summary>
    /// Retrieves all descendant nodes (offspring) of the specified tree node, including children, grandchildren, and so on.
    /// </summary>
    /// <typeparam name="T">The type of the value stored in the tree node.</typeparam>
    /// <param name="item">The tree node whose offspring are to be retrieved.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing all descendant nodes of the specified tree node.</returns>
    public static IEnumerable<TreeNode<T>> GetOffspring<T>(this TreeNode<T> item)
    {
        var children = item.GetChildren();
        foreach (var child in children)
        {
            yield return child;
            foreach (var grandChild in child.GetOffspring())
            {
                yield return grandChild;
            }
        }
    }
    /// <summary>
    /// Retrieves the sibling nodes of the specified tree node, excluding the node itself.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree node.
    /// </typeparam>
    /// <param name="item">
    /// The tree node for which to retrieve the siblings.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the sibling nodes of the specified tree node.
    /// If the node has no parent or no siblings, an empty collection is returned.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetBrothers<T>(this TreeNode<T> item) 
        => item.Parent?.GetChildren().Where(i => !item.Equals(i)) ?? [];
    /// <summary>
    /// Retrieves the uncles of the specified tree node. Uncles are the siblings of the node's parent.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree node.
    /// </typeparam>
    /// <param name="item">
    /// The tree node for which to retrieve the uncles.
    /// </param>
    /// <returns>
    /// A collection of tree nodes representing the uncles of the specified node. If the node has no parent or the parent has no siblings, an empty collection is returned.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetUncles<T>(this TreeNode<T> item) 
        => item.Parent?.GetBrothers().GetChildren() ?? [];
    /// <summary>
    /// Retrieves the nephews of the current tree node. Nephews are the children of the node's uncles.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree node.
    /// </typeparam>
    /// <param name="item">
    /// The current tree node for which to retrieve the nephews.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the nephews of the specified tree node.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetNephews<T>(this TreeNode<T> item) 
        => item.GetUncles().GetChildren();

    //TreeList extensions
    /// <summary>
    /// Filters the collection of tree nodes to include only those whose values match the specified selected items.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of tree nodes to filter.
    /// </param>
    /// <param name="selectedItems">
    /// The collection of values to match against the tree nodes' values.
    /// </param>
    /// <returns>
    /// A collection of tree nodes whose values are present in the <paramref name="selectedItems"/>.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetSelf<T>(this IEnumerable<TreeNode<T>> collection, IEnumerable<T> selectedItems) 
        => collection.Join(selectedItems, c => c.Value, i => i, (c, _) => c);
    /// <summary>
    /// Retrieves the tree nodes from the collection that match the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in the tree nodes.</typeparam>
    /// <param name="collection">The collection of tree nodes to search within.</param>
    /// <param name="item">The item to match against the values of the tree nodes.</param>
    /// <returns>
    /// A collection of <see cref="TreeNode{T}"/> objects from the input collection
    /// that have a value matching the specified item.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetSelf<T>(this IEnumerable<TreeNode<T>> collection, T item) => collection.GetSelf(
        [item]);
    /// <summary>
    /// Retrieves the parent nodes of the tree nodes in the specified collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of <see cref="TreeNode{T}"/> instances for which to retrieve the parent nodes.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the distinct parent nodes of the specified tree nodes.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetParents<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.Where(i => i.Parent != null).Select(i => i.Parent!).Distinct();
    /// <summary>
    /// Retrieves all ancestor nodes of the specified tree node collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of tree nodes for which to retrieve the ancestors.
    /// </param>
    /// <returns>
    /// A distinct collection of all ancestor nodes for the specified tree node collection.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetAncestors<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.SelectMany(i => i.GetAncestors()).Distinct();
    /// <summary>
    /// Retrieves the root nodes from a collection of tree nodes.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of <see cref="TreeNode{T}"/> instances from which to extract the root nodes.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the distinct root nodes from the provided collection.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetRoots<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.Select(i => i.GetRoot()).Distinct();
    /// <summary>
    /// Retrieves all bottom nodes (leaf nodes) from the specified collection of tree nodes.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of tree nodes from which to retrieve the bottom nodes.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing all bottom nodes (nodes without children) 
    /// from the specified collection.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetBottom<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.WithOffspring().Where(n => !n.Children.Any());
    /// <summary>
    /// Retrieves the direct children of each <see cref="TreeNode{T}"/> in the specified collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of <see cref="TreeNode{T}"/> instances from which to retrieve the children.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the distinct direct children of the nodes in the specified collection.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetChildren<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.SelectMany(c => c.GetChildren()).Distinct();
    /// <summary>
    /// Retrieves all descendant nodes (offspring) of the specified tree node, including its children, grandchildren, and so on.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in the tree nodes.</typeparam>
    /// <param name="collection"></param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing all descendant nodes of the specified tree node.</returns>
    public static IEnumerable<TreeNode<T>> GetOffspring<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.SelectMany(n => n.GetOffspring()).Distinct();
    /// <summary>
    /// Extends a collection of <see cref="TreeNode{T}"/> by including all their offspring nodes.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of <see cref="TreeNode{T}"/> to extend.
    /// </param>
    /// <returns>
    /// A collection of <see cref="TreeNode{T}"/> that includes the original nodes and all their offspring.
    /// </returns>
    public static IEnumerable<TreeNode<T>> WithOffspring<T>(this IEnumerable<TreeNode<T>> collection)
    {
        var list = collection as IList<TreeNode<T>> ?? collection.ToList();
        return list
            .Concat(list.GetOffspring())
            .Distinct();
    }
    /// <summary>
    /// Retrieves the sibling nodes (brothers) of each node in the specified collection.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of tree nodes for which to retrieve the siblings.
    /// </param>
    /// <returns>
    /// A distinct collection of sibling nodes for each node in the input collection.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetBrothers<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.SelectMany(i => i.GetBrothers()).Distinct();
    /// <summary>
    /// Retrieves the uncles of each node in the provided collection. 
    /// An uncle is defined as a sibling of a node's parent.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value stored in the tree nodes.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of tree nodes for which to retrieve the uncles.
    /// </param>
    /// <returns>
    /// A distinct collection of tree nodes representing the uncles of the nodes in the input collection.
    /// </returns>
    public static IEnumerable<TreeNode<T>> GetUncles<T>(this IEnumerable<TreeNode<T>> collection) 
        => collection.SelectMany(i => i.GetUncles()).Distinct();

    /// <summary>
    /// Reverses the hierarchy of the specified <see cref="TreeList{T}"/> by inverting the parent-child relationships.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the tree.</typeparam>
    /// <param name="tree">The <see cref="TreeList{T}"/> to reverse.</param>
    /// <returns>A new <see cref="TreeList{T}"/> with the hierarchy reversed.</returns>
    public static TreeList<T> ReverseTree<T>(this TreeList<T> tree)
    {
        var roots = tree.Where(n => !n.Children.Any());
        var rootValues = roots.Select(r => r.Value).Distinct().ToArray();
        var reverseTree = new TreeList<T>(tree.Options);

        void AddValue(T value, TreeNode<T>? parent = null)
        {
            var node = reverseTree.AddValue(value, parent)!;
            var children = tree.GetSelf(value).GetParents();
            var childValues = children.Select(n => n.Value).Distinct().ToArray();
            foreach (var child in childValues)
            {
                AddValue(child, node);
            }
        }

        foreach (var value in rootValues)
        {
            AddValue(value);
        }

        return reverseTree;
    }
}