# Regira TreeList

`Regira.TreeList` is a generic .NET library for building and navigating **hierarchical tree structures**. 
It supports both **one-to-many** and **many-to-many** parent-child relationships, provides rich navigation 
extension methods, and includes built-in protection against circular references.

## Core Concepts

### Classes & Interfaces

| Type | Purpose |
|------|---------|
| `TreeList<T>` | Main container — inherits `List<TreeNode<T>>` |
| `TreeNode<T>` | A single node holding a value and its children |
| `TreeView<T>` | Read-only view returning values in depth-first order |
| `ITreeNode<T>` | Interface for node access (`Value`, `Level`, `Parent`, `Children`, `Root`) |
| `InvalidChildException<T>` | Thrown when a value cannot be placed: an ancestor added as a child, or a value no root can reach |

### Node Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `T` | The wrapped object |
| `Level` | `int` | Depth in the tree (0 = root) |
| `Parent` | `TreeNode<T>?` | Immediate parent, or `null` for roots |
| `Root` | `TreeNode<T>?` | Top-most ancestor, or `null` for a root node |
| `Children` | `ICollection<TreeNode<T>>` | Direct children |

> `Root` exposes the top-most ancestor directly; the `GetRoot()` extension computes it by walking the `Parent` chain when you hold a bare `TreeNode<T>`.

## Installation

```xml
<PackageReference Include="Regira.TreeList" Version="6.*" />
```

## Building a Tree

### From a flat collection with a parent selector

```csharp no-compile
var people = new[]
{
    new Person { Id = 1, Name = "Alice", ParentId = null },
    new Person { Id = 2, Name = "Bob",   ParentId = 1 },
    new Person { Id = 3, Name = "Carol", ParentId = 1 },
};

// Single-parent selector
var tree = people.ToTreeList(p => people.FirstOrDefault(x => x.Id == p.ParentId));

Console.WriteLine(tree.Roots.Length);              // 1  (Alice)
Console.WriteLine(tree.Roots[0].Children.Count);   // 2  (Bob, Carol)
```

### From roots with a children selector (best performance)

```csharp no-compile
var roots = people.Where(p => p.ParentId == null);

var tree = people.ToTreeList(
    roots,
    node => people.Where(p => p.ParentId == node.Value.Id));
```

### Manual construction

```csharp
var tree = new TreeList<string>();
var root = tree.AddValue("root");
var child = tree.AddValue("child", root);
child!.AddChild("grandchild");
```

## Navigating a Tree

Once the tree is built every node exposes navigation extension methods:

```csharp no-compile
var node = tree.First(n => n.Value.Name == "Bob");

// Single-node navigation
var root      = node.GetRoot();         // Alice
var ancestors = node.GetAncestors();    // [Alice]
var children  = node.GetChildren();     // direct children of Bob
var offspring = node.GetOffspring();    // all descendants of Bob (recursive)
var siblings  = node.GetBrothers();     // Carol (same parent, excluding self)
var uncles    = node.GetUncles();       // children of Alice's siblings
var nephews   = node.GetNephews();      // children of uncles
```

Extension methods also work on **collections of nodes**:

```csharp no-compile
IEnumerable<TreeNode<Person>> subset = tree.Where(n => n.Level == 1);

var roots     = subset.GetRoots();      // root nodes reachable from subset
var ancestors = subset.GetAncestors();  // all ancestors (distinct)
var parents   = subset.GetParents();    // distinct parent nodes
var leaves    = tree.GetBottom();       // nodes with no children
var offspring = subset.GetOffspring();  // all descendants
var withSelf  = subset.WithOffspring(); // self + all descendants
```

## Ordering & Views

```csharp no-compile
// Depth-first traversal (default)
var ordered = tree.OrderByHierarchy();

// Depth-first with a custom sort key: roots, and the children of every node, sorted by the key.
// A parent always precedes its own children, whatever the key says.
var orderedByName = tree.OrderByHierarchy(n => n.Value.Name);

// Read-only view — values in depth-first order
TreeView<Person> view = tree.ToTreeView();
```

## Reversing a Tree

`ReverseTree` inverts all parent-child relationships.  
Leaf nodes become roots; the original root becomes a leaf.

```csharp no-compile
var reversed = tree.ReverseTree();
```

## Error Handling

By default the tree throws `InvalidChildException<T>` in two situations:

- **Adding an ancestor as a child** — `AddValue`, `AddChild` and `AddChildren` reject a value that already
  appears in the parent's ancestor chain.
- **Values no root can reach** — the parent-selector builds (`Fill(values, getParent)` /
  `Fill(values, getParents)` and the matching `ToTreeList` overloads) derive the roots from the values
  without a parent and then walk downwards. A value in a cycle has a parent, so it is never a root and the
  walk never arrives at it; the same goes for a value whose parent object is missing from the collection.
  Both are reported after the fill instead of quietly missing from the tree.

`ThrowOnError` turns both into a silent skip:

```csharp no-compile
var tree = new TreeList<Person>(new TreeList<Person>.TreeOptions
{
    EnableAutoCheck = true,   // validate before adding (default: true)
    ThrowOnError    = false   // return null / leave the value out instead of throwing (default: true)
});

var invalidNode = tree.AddValue(ancestor, descendantNode); // returns null
```

The children-selector build (`Fill(rootValues, getChildren)`) takes its roots from the caller, so it has no
unreachable values to report — a cycle it walks into is stopped by the ancestor check on the way down.

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/TreeList/)** — Overview and basic usage
1. [Examples](https://regira.github.io/Regira-Packages/src/TreeList/docs/examples.html) — FamilyTree (one-to-many) & CookbookTree (many-to-many)

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
