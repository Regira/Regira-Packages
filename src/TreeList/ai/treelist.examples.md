# TreeList — Example: Product Category Tree

> Context: A webshop displays categories in a hierarchical menu. Categories can have subcategories (one parent). The back-office shows a flat sorted list with depth indentation.

## Build the tree from a flat list

```csharp
using Regira.TreeList;

// Each Category has a nullable ParentId and a reference to its Parent
var tree = categories.ToTreeList(c => c.Parent!);
```

## Render a hierarchical menu

```csharp
var ordered = tree.OrderByHierarchy(n => n.Value.SortOrder);
foreach (var node in ordered)
{
    var indent = new string(' ', node.Level * 2);
    Console.WriteLine($"{indent}{node.Value.Title}");
}
```

## Find all subcategories of "Electronics"

```csharp
var electronicsNode = tree.First(n => n.Value.Slug == "electronics");
var allSubs         = electronicsNode.GetOffspring().Select(n => n.Value);
```

## Get breadcrumb for a category page

```csharp
public IEnumerable<Category> GetBreadcrumb(Category category)
{
    var node = tree.First(n => n.Value.Id == category.Id);
    return node.GetAncestors()        // root → direct parent
               .Append(node)
               .Select(n => n.Value);
}
```

## Build tree top-down (best performance when children are navigable)

```csharp
var rootCategories = categories.Where(c => c.ParentId == null);

var tree = categories.ToTreeList(
    rootCategories,
    node => categories.Where(c => c.ParentId == node.Value.Id));
```

## Get a flat, depth-first view of raw values

```csharp
TreeView<Category> view = tree.ToTreeView();
// view[0] is the first root category; view.Tree is the full TreeList
```

## Build a tree from EF Core rows (multi-parent join)

Self-referencing entities stored as parent/child join rows (multi-parent) come out of the database flat; the multi-parent `ToTreeList` overload reassembles them. With Regira Entities, tree endpoints fetch the flat rows through a mapped SQL function, then return the assembled tree depth-first:

```csharp no-compile
// rows: { ParentId, ChildId, Level, RootId } — e.g. from a recursive-CTE table-valued function
var items = await dbContext.GetCategoryOffspring(ids, maxLevel).ToListAsync();
// the selector returns each row's PARENT rows — an edge-row's parent is the row ending where it starts
var tree  = items.ToTreeList(x => items.FindAll(p => p.ChildId == x.ParentId));
return tree.ToTreeView();   // parent-before-children order for the client
```

> Full recipe (keyless projection, `HasDbFunction` mapping, recursive-CTE SQL, subtree SearchObject filters):
> `get_package(id: "Regira.Entities", section: "blueprints", heading: "Recursive entities")`.
