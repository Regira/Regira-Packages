# Regira TreeList — Examples

## Example 1: FamilyTree (one-to-many)

This example models a classic family tree where every person has at most one set of parents.

### Domain model

```csharp
public class FamilyMember
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<FamilyMember>? Parents { get; set; } = [];

    public override string ToString() => Name;
}
```

### Tree structure

```
Grandpa (level 0)
├── Father (level 1)
│   ├── Child1 (level 2)
│   │   └── GrandChild1 (level 3)
│   └── Child2 (level 2)
└── Uncle (level 1)
    └── Cousin (level 2)
        └── GrandChild2 (level 3)
```

### Building the tree

```csharp
var grandpa     = new FamilyMember { Id = 1, Name = "Grandpa" };
var father      = new FamilyMember { Id = 2, Name = "Father",     Parents = [grandpa] };
var uncle       = new FamilyMember { Id = 3, Name = "Uncle",      Parents = [grandpa] };
var child1      = new FamilyMember { Id = 4, Name = "Child1",     Parents = [father] };
var child2      = new FamilyMember { Id = 5, Name = "Child2",     Parents = [father] };
var cousin      = new FamilyMember { Id = 6, Name = "Cousin",     Parents = [uncle] };
var grandChild1 = new FamilyMember { Id = 7, Name = "GrandChild1", Parents = [child1] };
var grandChild2 = new FamilyMember { Id = 8, Name = "GrandChild2", Parents = [cousin] };

var allMembers = new[] { grandpa, father, uncle, child1, child2, cousin, grandChild1, grandChild2 };

// Option A — parent-collection selector (resolves hierarchy automatically)
var tree = new TreeList<FamilyMember>();
tree.Fill(allMembers, m => m.Parents ?? []);

// Option B — children selector (best performance)
var tree = allMembers.ToTreeList(
    new[] { grandpa },
    node => allMembers.Where(m => m.Parents?.Contains(node.Value) == true));
```

### Querying the tree

```csharp
// Roots
Console.WriteLine(tree.Roots.Length);              // 1
Console.WriteLine(tree.Roots[0].Value.Name);       // Grandpa

// Leaf nodes
var leaves = tree.GetBottom().Select(n => n.Value.Name);
// ["Child2", "GrandChild1", "GrandChild2"]

// Single-node navigation
var grandChild1Node = tree.First(n => n.Value == grandChild1);
var root      = grandChild1Node.GetRoot().Value.Name;   // "Grandpa"
var ancestors = grandChild1Node.GetAncestors()
                    .Select(n => n.Value.Name);          // ["Grandpa", "Father", "Child1"]

// Children / offspring
var fatherNode = tree.First(n => n.Value == father);
var children   = fatherNode.GetChildren()
                     .Select(n => n.Value.Name);         // ["Child1", "Child2"]
var offspring  = fatherNode.GetOffspring()
                     .Select(n => n.Value.Name);         // ["Child1", "GrandChild1", "Child2"]

// Siblings
var brothersOfFather = fatherNode.GetBrothers()
                           .Select(n => n.Value.Name);   // ["Uncle"]

// Uncles (children of parent's siblings)
var child1Node = tree.First(n => n.Value == child1);
var uncles     = child1Node.GetUncles()
                     .Select(n => n.Value.Name);         // ["Cousin"]

// Nephews (children of uncles)
var nephews    = child1Node.GetNephews()
                     .Select(n => n.Value.Name);         // ["GrandChild2"]

// Depth-first ordering
var ordered = tree.OrderByHierarchy()
                  .Select(n => n.Value.Name);
// ["Grandpa", "Father", "Child1", "GrandChild1", "Child2", "Uncle", "Cousin", "GrandChild2"]

// Depth-first ordering with a custom sort key per level
var orderedById = tree.OrderByHierarchy(n => n.Value.Id)
                      .Select(n => n.Value.Name);
// ["Grandpa", "Father", "Uncle", "Child1", "Child2", "Cousin", "GrandChild1", "GrandChild2"]

// Read-only view (values in depth-first order)
TreeView<FamilyMember> view = tree.ToTreeView();

// Reverse tree — leaves become roots
var reversed = tree.ReverseTree();
var reversedRoots = reversed.Roots.Select(n => n.Value.Name);
// ["Child2", "GrandChild1", "GrandChild2"]
```

---

## Example 2: CookbookTree (many-to-many)

This example models a cookbook where multiple recipes share the same ingredients,
and ingredients can themselves have sub-ingredients — creating a **many-to-many** hierarchy.

The same `Ingredient` object appears as a **separate tree node** under each recipe (or parent ingredient) 
that uses it, which is the key characteristic of a many-to-many tree.

### Domain model

```csharp
public interface ICookbookItem
{
    int Id { get; set; }
    string Name { get; set; }
}

public class Recipe : ICookbookItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public IList<Ingredient> Ingredients { get; set; } = [];

    public override string ToString() => Name;
}

public class Ingredient : ICookbookItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public IList<Ingredient> SubIngredients { get; set; } = [];

    public override string ToString() => Name;
}
```

### Tree structure

```
Pasta  (root, level 0)         Pizza  (root, level 0)         Salad  (root, level 0)
├── Dough      (level 1)        ├── Dough      (level 1)       ├── Tomato   (level 1)
│   ├── Flour  (level 2)        │   ├── Flour  (level 2)       ├── OliveOil (level 1)
│   │   └── Grain (level 3)     │   │   └── Grain (level 3)    └── Lettuce  (level 1)
│   │       └── Wheat (level 4) │   │       └── Wheat (level 4)
│   └── Water  (level 2)        │   └── Water  (level 2)
├── Tomato     (level 1)        ├── Tomato     (level 1)
└── OliveOil   (level 1)        ├── OliveOil   (level 1)
                                └── Mozzarella (level 1)
```

Total: **21 nodes** — the same ingredient value can appear multiple times as different nodes.

### Building the tree

```csharp
// Ingredients
var tomato     = new Ingredient { Id = 4,  Name = "Tomato" };
var oliveOil   = new Ingredient { Id = 5,  Name = "OliveOil" };
var flour      = new Ingredient { Id = 6,  Name = "Flour" };
var mozzarella = new Ingredient { Id = 7,  Name = "Mozzarella" };
var lettuce    = new Ingredient { Id = 8,  Name = "Lettuce" };
var dough      = new Ingredient { Id = 9,  Name = "Dough" };
var water      = new Ingredient { Id = 10, Name = "Water" };
var grain      = new Ingredient { Id = 11, Name = "Grain" };
var wheat      = new Ingredient { Id = 12, Name = "Wheat" };

// 4-level ingredient hierarchy: Dough → Flour → Grain → Wheat
grain.SubIngredients = [wheat];
flour.SubIngredients = [grain];
dough.SubIngredients = [flour, water];

// Recipes
var pasta = new Recipe { Id = 1, Name = "Pasta", Ingredients = [dough, tomato, oliveOil] };
var pizza = new Recipe { Id = 2, Name = "Pizza", Ingredients = [dough, tomato, oliveOil, mozzarella] };
var salad = new Recipe { Id = 3, Name = "Salad", Ingredients = [tomato, oliveOil, lettuce] };

Recipe[] recipes = [pasta, pizza, salad];

// Build tree using children selector (best performance, handles many-to-many naturally)
var tree = new TreeList<ICookbookItem>();
tree.Fill(recipes, node => node.Value switch
{
    Recipe     r => r.Ingredients.Cast<ICookbookItem>(),
    Ingredient i => i.SubIngredients.Cast<ICookbookItem>(),
    _            => Enumerable.Empty<ICookbookItem>()
});
```

### Querying the tree

```csharp
// Roots — one node per recipe
Console.WriteLine(tree.Roots.Length);              // 3
// ["Pasta", "Pizza", "Salad"]

// Total node count (shared ingredients appear multiple times as nodes)
Console.WriteLine(tree.Count);                     // 21

// Many-to-many: find all nodes for a shared ingredient
var tomatoNodes = tree.GetSelf(tomato);
Console.WriteLine(tomatoNodes.Count());            // 3 (Pasta, Pizza, Salad each have one)

var flourNodes = tree.GetSelf(flour);
Console.WriteLine(flourNodes.Count());             // 2 (Pasta and Pizza each have one via Dough)

// Parents of all Tomato nodes → the three recipe nodes
var tomatoParents = tree.GetSelf(tomato)
                        .GetParents()
                        .Select(n => n.Value.Name);
// ["Pasta", "Pizza", "Salad"]

// Ancestors of all Flour nodes → their parent recipe nodes
var flourAncestors = tree.GetSelf(flour)
                         .GetAncestors()
                         .Select(n => n.Value.Name)
                         .Distinct();
// ["Pasta", "Pizza", "Dough"]   (Dough appears once per recipe path)

// Leaf nodes (no sub-ingredients)
var leaves = tree.GetBottom().Select(n => n.Value.Name).Distinct();
// ["Wheat", "Water", "Tomato", "OliveOil", "Mozzarella", "Lettuce"]

// Roots reachable from Lettuce nodes
var lettuceRoots = tree.GetSelf(lettuce)
                       .GetRoots()
                       .Select(n => n.Value.Name);
// ["Salad"]

// All descendants of a recipe
var pastaNode = tree.First(n => n.Value == pasta);
var pastaOffspring = pastaNode.GetOffspring()
                              .Select(n => n.Value.Name);
// ["Dough", "Flour", "Grain", "Wheat", "Water", "Tomato", "OliveOil"]

// Depth-first ordering
var ordered = tree.OrderByHierarchy()
                  .Select(n => n.Value.Name);
// ["Pasta", "Dough", "Flour", "Grain", "Wheat", "Water", "Tomato", "OliveOil",
//  "Pizza", "Dough", "Flour", "Grain", "Wheat", "Water", "Tomato", "OliveOil", "Mozzarella",
//  "Salad", "Tomato", "OliveOil", "Lettuce"]
```

---

## Overview

1. [Index](../README.md) — Overview and basic usage
1. **[Examples](examples.md)** — FamilyTree (one-to-many) & CookbookTree (many-to-many)
