# Regira TreeList AI Agent Instructions

> A generic .NET library for building and navigating hierarchical tree structures. Supports one-to-many and many-to-many parent-child relationships, rich navigation extension methods, and built-in circular reference protection.

---

## Installation

```xml
<PackageReference Include="Regira.TreeList" Version="6.*" />
```

---

## Namespaces

| Namespace | Contains |
|---|---|
| `Regira.TreeList` | `TreeList<T>`, `TreeNode<T>`, `TreeView<T>`, `InvalidChildException<T>`, `TreeListExtensions` |
| `using Regira.TreeList.Abstractions` | `ITreeNode<T>`, `TreeNodeBase<T>` |

For most use cases a single `using` is sufficient:

```csharp
using Regira.TreeList;
```

Add the abstractions namespace only when you need to reference `ITreeNode<T>` explicitly (e.g. in a children-selector lambda):

```csharp
using Regira.TreeList.Abstractions;

var tree = allItems.ToTreeList(
    roots,
    (ITreeNode<MyType> node) => node.Value.Children);
```

---

## Core Types

### `ITreeNode<T>` — `using Regira.TreeList.Abstractions`

Read-only interface for any tree node.

| Property | Type | Description |
|---|---|---|
| `Value` | `T` | The value stored in this node |
| `Level` | `int` | Depth in the tree; roots are level `0` |
| `Root` | `TreeNode<T>?` | The top-most ancestor node |
| `Parent` | `TreeNode<T>?` | Direct parent; `null` for root nodes |
| `Children` | `ICollection<TreeNode<T>>` | Direct child nodes |

---

### `TreeNode<T>` — `using Regira.TreeList`

Concrete node class. Inherits `ITreeNode<T>` and adds:

| Member | Signature | Description |
|---|---|---|
| `Tree` | `TreeList<T>` | The `TreeList<T>` this node belongs to |
| `AddChild` | `TreeNode<T>? AddChild(T value)` | Adds a single child node and returns it; `null` when the value was rejected (circular reference + `ThrowOnError = false`) |
| `AddChildren` | `IEnumerable<TreeNode<T>> AddChildren(IEnumerable<T> values)` | Adds multiple children; skips (or throws) on circular reference |
| `RemoveChild` | `bool RemoveChild(TreeNode<T> child)` | Removes a direct child node (and all its descendants) from the tree |

`TreeNode<T>` cannot be instantiated directly — nodes are always created via `TreeList<T>`.

---

### `TreeList<T>` — `using Regira.TreeList`

The main container. Inherits `List<TreeNode<T>>`.

#### Constructors

```csharp
new TreeList<T>(TreeOptions? options = null)
new TreeList<T>(int capacity, TreeOptions? options = null)
new TreeList<T>(IEnumerable<T> collection, TreeOptions? options = null)          // each item becomes a root node
new TreeList<T>(IEnumerable<TreeNode<T>> collection, TreeOptions? options = null) // preserves existing parent/child links
```

#### Properties

| Property | Type | Description |
|---|---|---|
| `Options` | `TreeOptions?` | The options instance used on construction |
| `EnableAutoCheck` | `bool` | Whether circular-reference checks are performed on add |
| `ThrowOnError` | `bool` | Whether to throw `InvalidChildException<T>` on circular reference (vs. silently skip) |
| `Roots` | `TreeNode<T>[]` | All root nodes (nodes with no parent) |

#### Methods

| Signature | Description |
|---|---|
| `TreeNode<T>? AddValue(T value, TreeNode<T>? parent = null)` | Adds a value as a child of `parent`, or as a new root when `parent` is `null`. Returns `null` if the add is rejected (circular reference + `ThrowOnError = false`). |
| `IEnumerable<TreeNode<T>> AddValues(IEnumerable<T> values, TreeNode<T>? parent = null)` | Adds multiple values under an optional parent. |
| `void Fill(IEnumerable<T> values, Func<T, T> getParent)` | Populates the tree from a flat list using a single-parent selector per item. Items whose `getParent` returns `null` become roots. Throws `InvalidChildException<T>` when values remain that no root can reach. |
| `void Fill(IEnumerable<T> values, Func<T, IEnumerable<T>> getParents)` | Populates using a multi-parent selector. **Lesser performance** — prefer the children-selector overload when possible. Throws `InvalidChildException<T>` when values remain that no root can reach. |
| `void Fill(IEnumerable<T> rootValues, Func<ITreeNode<T>, IEnumerable<T>> getChildren)` | Populates by starting from known roots and traversing children top-down. **Best performance** — use this when you can enumerate children for each node. Clears the list before filling. |
| `new void RemoveAt(int index)` | Removes the node at `index`, its parent link, and all descendants. |
| `new bool Remove(TreeNode<T> item)` | Removes a specific node (and descendants). Returns `true` when found. |
| `int Remove(T value)` | Removes all nodes whose `Value` equals `value`. Returns the count removed. |
| `int RemoveAll(IEnumerable<TreeNode<T>> items)` | Removes all nodes in the collection. Returns count removed. |
| `int RemoveAll(Func<TreeNode<T>, bool> predicate)` | Removes all nodes matching the predicate. Returns count removed. |
| `bool IsValidChild(TreeNode<T> parent, T child)` | Returns `true` when `child` does not appear in `parent`'s ancestor chain (i.e. adding it would not create a cycle). |

---

### `TreeList<T>.TreeOptions`

```csharp
public class TreeOptions
{
    public bool EnableAutoCheck { get; set; } = true;   // validate against circular references on add
    public bool ThrowOnError    { get; set; } = true;   // throw InvalidChildException<T> when a circular ref is detected
}
```

When `EnableAutoCheck = true` and `ThrowOnError = false`, `AddValue` / `AddChild` silently return `null` / skip the bad value instead of throwing.

---

### `TreeView<T>` — `using Regira.TreeList`

A `ReadOnlyCollection<T>` of **raw values** in depth-first (hierarchical) order.

| Member | Type | Description |
|---|---|---|
| `Tree` | `TreeList<T>` | Back-reference to the source `TreeList<T>` |

Created with `tree.ToTreeView()`. The values are ordered by `OrderByHierarchy()` during construction.

---

### `InvalidChildException<T>` — `using Regira.TreeList`

Thrown in two situations:

- a value that is an ancestor of `parent` is added as a child (circular reference) — `ParentNode` holds the parent
- a parents-selector `Fill` ends with values no root can reach — `ParentNode` is `null` and `Child` holds the first offender

| Property | Type |
|---|---|
| `ParentNode` | `TreeNode<T>?` |
| `Child` | `T` |

---

## Building a Tree

Four strategies ranked by performance and use case.

### Strategy 1 — Children-selector (best performance, recommended)

Use when you know the root items and can enumerate children per node top-down.

**Method:** `Fill(IEnumerable<T> rootValues, Func<ITreeNode<T>, IEnumerable<T>> getChildren)`
**Extension:** `ToTreeList<T>(this IEnumerable<T> collection, IEnumerable<T> roots, Func<ITreeNode<T>, IEnumerable<T>> getChildren, TreeOptions? options = null)`

```csharp
// Via Fill (mutates an existing TreeList)
var tree = new TreeList<string>(directories.Length);
tree.Fill(
    rootPaths,
    parentNode => directories.Where(p => Path.GetDirectoryName(p) == parentNode.Value));

// Via ToTreeList extension (creates a new TreeList)
var tree = allItems.ToTreeList(
    rootItems,
    (ITreeNode<ICookbookItem> node) => node.Value is Recipe r
        ? r.Ingredients.Cast<ICookbookItem>()
        : node.Value is Ingredient i
            ? i.SubIngredients.Cast<ICookbookItem>()
            : Enumerable.Empty<ICookbookItem>());
```

### Strategy 2 — Single-parent selector

Use when each item has at most one parent and you can return it directly.

**Method:** `Fill(IEnumerable<T> values, Func<T, T> getParent)`
**Extension:** `ToTreeList<T>(this IEnumerable<T> collection, Func<T, T> getParent, TreeOptions? options = null)`

```csharp
// Items where getParent returns null become roots
var tree = persons.ToTreeList(p => p.Parent!);
// equivalent:
var tree = new TreeList<Person>();
tree.Fill(persons, p => p.Parent!);
```

### Strategy 3 — Multi-parent selector

Use when items can have multiple parents (many-to-many).

**Method:** `Fill(IEnumerable<T> values, Func<T, IEnumerable<T>> getParents)`
**Extension:** `ToTreeList<T>(this IEnumerable<T> collection, Func<T, IEnumerable<T>> fkSelector, TreeOptions? options = null)`

```csharp
var tree = members.ToTreeList(m => m.Parents ?? []);
// equivalent:
var tree = new TreeList<FamilyMember>();
tree.Fill(members, m => m.Parents ?? []);
```

> ⚠ Lesser performance than the children-selector strategy. Prefer strategy 1 when possible.

### Strategy 4 — Manual construction

Use when building a tree programmatically, node by node.

```csharp
var tree = new TreeList<string>();

// Add root via tree
var root = tree.AddValue("root");       // returns TreeNode<string>

// Add children via tree (parent required)
var child = tree.AddValue("child", root);

// Add children via node
var grandChild = root!.AddChild("grandchild");
var siblings   = root.AddChildren(["a", "b", "c"]);
```

---

## Single-Node Navigation Extensions

All operate on a single `TreeNode<T>` instance.

| Signature | Returns | Notes |
|---|---|---|
| `GetRoot<T>(this TreeNode<T> item) → TreeNode<T>` | Root ancestor | Returns `self` when the node is already a root |
| `GetAncestors<T>(this TreeNode<T> item) → IEnumerable<TreeNode<T>>` | All ancestors | Ordered **root → direct parent**; empty for root nodes |
| `GetChildren<T>(this TreeNode<T> item) → IEnumerable<TreeNode<T>>` | Direct children | Same as `node.Children` |
| `GetOffspring<T>(this TreeNode<T> item) → IEnumerable<TreeNode<T>>` | All descendants recursively | Depth-first |
| `GetBrothers<T>(this TreeNode<T> item) → IEnumerable<TreeNode<T>>` | Siblings (same parent, excluding self) | Empty for root nodes or only-children |
| `GetUncles<T>(this TreeNode<T> item) → IEnumerable<TreeNode<T>>` | Children of parent's siblings | Empty when parent has no siblings |
| `GetNephews<T>(this TreeNode<T> item) → IEnumerable<TreeNode<T>>` | Children of uncles | Empty when there are no uncles |

```csharp
// Given: Grandpa → Father → Child1 → GrandChild1
var grandChild1Node = tree.First(n => n.Value == grandChild1);

grandChild1Node.GetRoot();        // node for Grandpa
grandChild1Node.GetAncestors();   // [Grandpa, Father, Child1]  (root → parent order)
grandChild1Node.GetBrothers();    // other children of Child1
grandChild1Node.GetUncles();      // children of Child1's siblings
grandChild1Node.GetNephews();     // children of those uncles
```

---

## Collection Navigation Extensions

All operate on `IEnumerable<TreeNode<T>>` and return distinct results.

| Signature | Returns |
|---|---|
| `GetSelf<T>(this IEnumerable<TreeNode<T>> collection, T item) → IEnumerable<TreeNode<T>>` | Nodes whose `Value` equals `item` |
| `GetSelf<T>(this IEnumerable<TreeNode<T>> collection, IEnumerable<T> selectedItems) → IEnumerable<TreeNode<T>>` | Nodes whose `Value` is in `selectedItems` |
| `GetParents<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct direct parents of all nodes in the collection |
| `GetAncestors<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct all ancestors across all nodes |
| `GetRoots<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct root node for each node |
| `GetBottom<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Leaf nodes (no children) from all descendants |
| `GetChildren<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct direct children of all nodes |
| `GetOffspring<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct all descendants of all nodes |
| `WithOffspring<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Original nodes **plus** all their descendants (distinct) |
| `GetBrothers<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct siblings of all nodes |
| `GetUncles<T>(this IEnumerable<TreeNode<T>> collection) → IEnumerable<TreeNode<T>>` | Distinct uncles of all nodes |

```csharp
// Find nodes by value (important for many-to-many: same value = multiple nodes)
var tomatoNodes = tree.GetSelf(tomato);      // may return > 1 node if tomato appears under multiple parents

// Chain navigation
var selectedRecipes = tree.GetSelf(new ICookbookItem[] { pasta, pizza });
var allIngredients  = selectedRecipes.GetOffspring();
var leafIngredients = selectedRecipes.WithOffspring().GetBottom();

// Trace upwards
var parentRecipes = tree.GetSelf(flour).GetRoots();  // root recipes that contain flour anywhere in their subtree
```

---

## `ToTreeList` Extension Overloads

All live in `TreeListExtensions`. Use the overload that matches your data shape.

| Overload | When to use |
|---|---|
| `ToTreeList<T>(this IEnumerable<TreeNode<T>> collection)` | Re-wrap existing nodes into a new `TreeList<T>` |
| `ToTreeList<T>(this IEnumerable<T> collection)` | All items become root nodes — no hierarchy |
| `ToTreeList<T>(this IEnumerable<T> collection, Func<T,T> getParent, TreeOptions? options = null)` | Single-parent selector |
| `ToTreeList<T>(this IEnumerable<T> collection, Func<T,IEnumerable<T>> fkSelector, TreeOptions? options = null)` | Multi-parent selector |
| `ToTreeList<T>(this IEnumerable<T> collection, IEnumerable<T> roots, Func<ITreeNode<T>,IEnumerable<T>> getChildren, TreeOptions? options = null)` | Children-selector with explicit roots — **recommended** |

---

## `ToTreeView`

```csharp
TreeView<T> ToTreeView<T>(this TreeList<T> tree)
```

Returns a `ReadOnlyCollection<T>` of raw values in hierarchical (depth-first) order.
`view.Tree` gives back the source `TreeList<T>`.

```csharp
var view = tree.ToTreeView();
// view[0] is the first root's value
// view.Tree == tree
```

---

## `OrderByHierarchy`

```csharp
IEnumerable<TreeNode<T>> OrderByHierarchy<T>(this IEnumerable<TreeNode<T>> collection)
IEnumerable<TreeNode<T>> OrderByHierarchy<T, TKey>(this IEnumerable<TreeNode<T>> collection, Func<TreeNode<T>, TKey>? keySelector = null)
```

Returns nodes in depth-first order: each root is immediately followed by **all** its offspring before the next root.
When `keySelector` is provided, the roots and the children of every node are sorted by that key — level by level,
never across levels, so a deep node never overtakes its own parent.

```csharp
// Insertion order within siblings
var ordered = tree.OrderByHierarchy();

// Sort siblings alphabetically by name
var sorted = tree.OrderByHierarchy(n => n.Value.Name);
```

> The parent is guaranteed to appear before all its children in the result.

---

## `ReverseTree`

```csharp
TreeList<T> ReverseTree<T>(this TreeList<T> tree)
```

Returns a **new** `TreeList<T>` with the hierarchy inverted: leaf nodes become roots, root nodes become
leaves. In many-to-many trees, duplicate values across subtrees are collapsed — each distinct value
appears once in the reversed tree.

```csharp
var reversed = tree.ReverseTree();
// Original roots are now leaf nodes (no children)
// Original leaf nodes are now root nodes
```

---

## Error Handling & Circular Reference Detection

By default (`EnableAutoCheck = true`, `ThrowOnError = true`), `InvalidChildException<T>` is thrown when

1. a value that already appears in a node's ancestor chain is added as a child, or
2. a **parents-selector** build (`Fill(values, getParent)` / `Fill(values, getParents)` and the matching
   `ToTreeList` overloads) leaves values that no root can reach.

Case 2 covers the whole cyclic input: those values all have a parent, so none of them is picked as a root,
and the downward walk never arrives at them. It equally covers a value whose parent object is not part of
`values`. Both used to be silently absent from the tree.

The **children-selector** build takes its roots from the caller and therefore has nothing unreachable to
report; a cycle it walks into is caught by the ancestor check on the way down (case 1).

```csharp
// Default — throws
person1.Parent = person0;
person0.Parent = person1;
// throws InvalidChildException<Person>: neither person is a root, so neither is reachable
persons.ToTreeList(p => p.Parent!);

// Silent — skip bad values, return null
var options = new TreeList<Person>.TreeOptions
{
    EnableAutoCheck = true,
    ThrowOnError = false
};

// The same build now just leaves the unreachable values out
var partial = persons.ToTreeList(p => p.Parent!, options);
Assert.That(partial, Is.Empty);

var tree  = new TreeList<Person>(options);
var root  = tree.AddValue(person0);
var child = tree.AddValue(person1, root);
var bad   = tree.AddValue(person0, child);  // person0 is an ancestor → returns null
Assert.That(bad, Is.Null);
```

`AddChild` follows `AddValue` here: it returns `null` for a rejected value rather than throwing.

> Compare `tree.Count` against your input count only as a sanity check, never as a cycle test — a value with
> several parents occupies one node per parent, so `Count` legitimately exceeds the number of values.

Use `tree.IsValidChild(parent, candidate)` to pre-check before adding:

```csharp
if (tree.IsValidChild(parentNode, candidateValue))
    parentNode.AddChild(candidateValue);
```
