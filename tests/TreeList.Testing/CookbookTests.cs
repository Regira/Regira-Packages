using Regira.TreeList;
using Regira.TreeList.Abstractions;
using TreeList.Testing.Infrastructure;

namespace TreeList.Testing;

/// <summary>
/// Unit tests for <see cref="TreeListExtensions"/> using a cookbook scenario (many-to-many relationships).
/// <para>
/// An ingredient can be used in multiple recipes, and a recipe has many ingredients.
/// Ingredients can also have sub-ingredients, creating a 4-level-deep hierarchy below each recipe root.
/// The many-to-many relationship emerges because the same <see cref="Ingredient"/> object
/// is referenced by multiple recipes or parent ingredients, producing multiple tree nodes for the same value.
/// </para>
/// <para>
/// Cookbook used across all tests (levels 0–4):
/// <code>
/// Pasta  (root, level 0)           Pizza  (root, level 0)           Salad  (root, level 0)
/// ├── Dough          (level 1)      ├── Dough          (level 1)     ├── Tomato   (level 1)
/// │   ├── Flour      (level 2)      │   ├── Flour      (level 2)     ├── OliveOil (level 1)
/// │   │   └── Grain  (level 3)      │   │   └── Grain  (level 3)     └── Lettuce  (level 1)
/// │   │       └── Wheat (level 4)   │   │       └── Wheat (level 4)
/// │   └── Water      (level 2)      │   └── Water      (level 2)
/// ├── Tomato         (level 1)      ├── Tomato         (level 1)
/// └── OliveOil       (level 1)      ├── OliveOil       (level 1)
///                                   └── Mozzarella     (level 1)
/// </code>
/// Dough, Flour, Grain, Wheat, Water appear under both Pasta and Pizza (many-to-many via Dough).
/// Tomato and OliveOil appear under all three recipes.
/// Mozzarella appears only under Pizza.
/// Lettuce appears only under Salad.
/// </para>
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CookbookTests
{
    private class CookbookData
    {
        public TreeList<ICookbookItem> Tree { get; init; } = null!;
        public Recipe Pasta { get; init; } = null!;
        public Recipe Pizza { get; init; } = null!;
        public Recipe Salad { get; init; } = null!;
        public Ingredient Dough { get; init; } = null!;
        public Ingredient Tomato { get; init; } = null!;
        public Ingredient OliveOil { get; init; } = null!;
        public Ingredient Flour { get; init; } = null!;
        public Ingredient Water { get; init; } = null!;
        public Ingredient Grain { get; init; } = null!;
        public Ingredient Wheat { get; init; } = null!;
        public Ingredient Mozzarella { get; init; } = null!;
        public Ingredient Lettuce { get; init; } = null!;
        public ICookbookItem[] AllItems { get; init; } = null!;
        public Recipe[] Recipes { get; init; } = null!;
        /// <summary>All ingredient objects (at every level).</summary>
        public Ingredient[] Ingredients { get; init; } = null!;
        /// <summary>Leaf ingredients: those that have no sub-ingredients and therefore appear as leaf tree nodes.</summary>
        public Ingredient[] LeafIngredients { get; init; } = null!;
    }

    private static CookbookData CreateData()
    {
        var pasta = new Recipe { Id = 1, Name = "Pasta" };
        var pizza = new Recipe { Id = 2, Name = "Pizza" };
        var salad = new Recipe { Id = 3, Name = "Salad" };

        var tomato = new Ingredient { Id = 4, Name = "Tomato" };
        var oliveOil = new Ingredient { Id = 5, Name = "OliveOil" };
        var flour = new Ingredient { Id = 6, Name = "Flour" };
        var mozzarella = new Ingredient { Id = 7, Name = "Mozzarella" };
        var lettuce = new Ingredient { Id = 8, Name = "Lettuce" };
        var dough = new Ingredient { Id = 9, Name = "Dough" };
        var water = new Ingredient { Id = 10, Name = "Water" };
        var grain = new Ingredient { Id = 11, Name = "Grain" };
        var wheat = new Ingredient { Id = 12, Name = "Wheat" };

        // 4-level sub-ingredient hierarchy (below the recipe root):
        // level 1: Dough
        // level 2: Flour, Water  (sub-ingredients of Dough)
        // level 3: Grain         (sub-ingredient of Flour)
        // level 4: Wheat         (sub-ingredient of Grain)
        grain.SubIngredients = [wheat];
        flour.SubIngredients = [grain];
        dough.SubIngredients = [flour, water];

        pasta.Ingredients = [dough, tomato, oliveOil];
        pizza.Ingredients = [dough, tomato, oliveOil, mozzarella];
        salad.Ingredients = [tomato, oliveOil, lettuce];

        ICookbookItem[] allItems = [pasta, pizza, salad, dough, tomato, oliveOil, flour, mozzarella, lettuce, water, grain, wheat];
        Recipe[] recipes = [pasta, pizza, salad];
        Ingredient[] ingredients = [dough, tomato, oliveOil, flour, mozzarella, lettuce, water, grain, wheat];
        // Leaf ingredients have no sub-ingredients and appear as tree leaf nodes
        Ingredient[] leafIngredients = [wheat, water, tomato, oliveOil, mozzarella, lettuce];

        // Build tree: each recipe is a root; traverse ingredient sub-ingredients recursively (many-to-many)
        var tree = new TreeList<ICookbookItem>();
        tree.Fill(recipes, node => node.Value is Recipe r
            ? r.Ingredients.Cast<ICookbookItem>()
            : node.Value is Ingredient i
                ? i.SubIngredients.Cast<ICookbookItem>()
                : Enumerable.Empty<ICookbookItem>());

        return new CookbookData
        {
            Tree = tree,
            Pasta = pasta,
            Pizza = pizza,
            Salad = salad,
            Dough = dough,
            Tomato = tomato,
            OliveOil = oliveOil,
            Flour = flour,
            Water = water,
            Grain = grain,
            Wheat = wheat,
            Mozzarella = mozzarella,
            Lettuce = lettuce,
            AllItems = allItems,
            Recipes = recipes,
            Ingredients = ingredients,
            LeafIngredients = leafIngredients
        };
    }

    // ─── ToTreeList overloads ──────────────────────────────────────────────────

    [Test]
    public void ToTreeList_WithManyToManyParentSelector_CorrectNodeCount()
    {
        var data = CreateData();

        // Pasta  subtree: Pasta + Dough + Flour + Grain + Wheat + Water + Tomato + OliveOil  = 8 nodes
        // Pizza  subtree: Pizza + Dough + Flour + Grain + Wheat + Water + Tomato + OliveOil + Mozzarella = 9 nodes
        // Salad  subtree: Salad + Tomato + OliveOil + Lettuce = 4 nodes  →  total 21
        Assert.That(data.Tree.Count, Is.EqualTo(21));
    }

    [Test]
    public void ToTreeList_WithManyToManyParentSelector_RecipesAreRoots()
    {
        var data = CreateData();

        Assert.That(data.Tree.Roots, Has.Length.EqualTo(3));
        Assert.That(data.Tree.Roots.Select(n => n.Value), Is.EquivalentTo(data.Recipes));
    }

    [Test]
    public void ToTreeList_WithChildrenSelector_BuildsEquivalentTree()
    {
        var data = CreateData();
        var allItems = data.AllItems;
        var recipes = data.Recipes;

        var tree = allItems.ToTreeList(
            recipes,
            (ITreeNode<ICookbookItem> node) => node.Value is Recipe r
                ? r.Ingredients.Cast<ICookbookItem>()
                : node.Value is Ingredient i
                    ? i.SubIngredients.Cast<ICookbookItem>()
                    : Enumerable.Empty<ICookbookItem>());

        Assert.That(tree.Count, Is.EqualTo(data.Tree.Count));
        Assert.That(tree.Roots.Select(n => n.Value), Is.EquivalentTo(recipes));
    }

    // ─── Many-to-many: same ingredient under multiple recipes ─────────────────

    [Test]
    public void GetSelf_TomatoAppearsUnderAllThreeRecipes()
    {
        var data = CreateData();

        // Tomato is used in Pasta, Pizza, Salad → 3 tree nodes, all with Value == tomato
        var tomatoNodes = data.Tree.GetSelf(data.Tomato).ToArray();

        Assert.That(tomatoNodes, Has.Length.EqualTo(3));
        Assert.That(tomatoNodes.All(n => n.Value == data.Tomato), Is.True);
    }

    [Test]
    public void GetSelf_FlourAppearsUnderTwoRecipes()
    {
        var data = CreateData();

        // Flour is a sub-ingredient of Dough; Dough appears under Pasta and Pizza → 2 Flour nodes
        var flourNodes = data.Tree.GetSelf(data.Flour).ToArray();

        Assert.That(flourNodes, Has.Length.EqualTo(2));
    }

    [Test]
    public void GetSelf_MozzarellaAppearsUnderOneRecipe()
    {
        var data = CreateData();

        var mozzarellaNodes = data.Tree.GetSelf(data.Mozzarella).ToArray();

        Assert.That(mozzarellaNodes, Has.Length.EqualTo(1));
    }

    [Test]
    public void GetSelf_MultipleIngredients_ReturnsAllMatchingNodes()
    {
        var data = CreateData();

        // Mozzarella (1 node) + Lettuce (1 node)
        var nodes = data.Tree.GetSelf(new ICookbookItem[] { data.Mozzarella, data.Lettuce }).ToArray();

        Assert.That(nodes, Has.Length.EqualTo(2));
        Assert.That(nodes.Select(n => n.Value), Is.EquivalentTo(new[] { data.Mozzarella, data.Lettuce }));
    }

    // ─── GetParents / GetAncestors ────────────────────────────────────────────

    [Test]
    public void GetParents_ForTomatoNodes_ReturnsAllThreeRecipes()
    {
        var data = CreateData();

        var parentValues = data.Tree.GetSelf(data.Tomato).GetParents().Select(n => n.Value).ToArray();

        Assert.That(parentValues, Is.EquivalentTo(data.Recipes));
    }

    [Test]
    public void GetParents_ForRecipeNodes_IsEmpty()
    {
        var data = CreateData();

        var parents = data.Tree.Roots.GetParents().ToArray();

        Assert.That(parents, Is.Empty);
    }

    [Test]
    public void GetAncestors_ForIngredientNodes_ReturnsParentRecipes()
    {
        var data = CreateData();

        // Tomato is a direct child of each recipe (depth 1), so its only ancestors are the recipe nodes
        var ancestorValues = data.Tree.GetSelf(data.Tomato).GetAncestors().Select(n => n.Value).ToArray();

        Assert.That(ancestorValues, Is.EquivalentTo(data.Recipes));
    }

    // ─── GetRoots ─────────────────────────────────────────────────────────────

    [Test]
    public void GetRoots_ForAllNodes_ReturnsRecipes()
    {
        var data = CreateData();

        var roots = data.Tree.GetRoots().Select(n => n.Value).ToArray();

        Assert.That(roots, Is.EquivalentTo(data.Recipes));
    }

    [Test]
    public void GetRoots_ForIngredientSubset_ReturnsCorrectRecipeRoots()
    {
        var data = CreateData();
        // All Lettuce nodes trace back to Salad only
        var lettuceNodes = data.Tree.GetSelf(data.Lettuce);

        var roots = lettuceNodes.GetRoots().Select(n => n.Value).ToArray();

        Assert.That(roots, Is.EquivalentTo(new[] { data.Salad }));
    }

    // ─── GetBottom ────────────────────────────────────────────────────────────

    [Test]
    public void GetBottom_ForTree_ReturnsAllIngredientNodes()
    {
        var data = CreateData();

        var bottomNodes = data.Tree.GetBottom().ToArray();

        // Leaf nodes per recipe subtree:
        //   Pasta:  Wheat, Water, Tomato, OliveOil       = 4
        //   Pizza:  Wheat, Water, Tomato, OliveOil, Mozzarella = 5
        //   Salad:  Tomato, OliveOil, Lettuce            = 3
        //   Total = 12
        Assert.That(bottomNodes, Has.Length.EqualTo(12));
        Assert.That(bottomNodes.All(n => !n.Children.Any()), Is.True);
    }

    [Test]
    public void GetBottom_IngredientValuesMatchExpected()
    {
        var data = CreateData();

        var bottomValues = data.Tree.GetBottom().Select(n => n.Value).Distinct().ToArray();

        // Only leaf-level ingredients appear at the bottom of the tree
        Assert.That(bottomValues, Is.EquivalentTo(data.LeafIngredients));
    }

    // ─── GetChildren ──────────────────────────────────────────────────────────

    [Test]
    public void GetChildren_ForPasta_ContainsPastaIngredients()
    {
        var data = CreateData();
        var pastaNode = data.Tree.Roots.First(n => n.Value == data.Pasta);

        var childValues = pastaNode.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(childValues, Is.EquivalentTo(new[] { data.Dough, data.Tomato, data.OliveOil }));
    }

    [Test]
    public void GetChildren_ForPizza_ContainsFourIngredients()
    {
        var data = CreateData();
        var pizzaNode = data.Tree.Roots.First(n => n.Value == data.Pizza);

        var childValues = pizzaNode.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(childValues, Is.EquivalentTo(new[] { data.Dough, data.Tomato, data.OliveOil, data.Mozzarella }));
    }

    [Test]
    public void GetChildren_ForCollection_ReturnsAllDirectChildren()
    {
        var data = CreateData();
        var pastaAndSaladNodes = data.Tree.Roots.Where(n => n.Value == data.Pasta || n.Value == data.Salad);

        var childValues = pastaAndSaladNodes.GetChildren().Select(n => n.Value).ToArray();

        // Pasta: Tomato, OliveOil, Flour; Salad: Tomato, OliveOil, Lettuce (distinct nodes but duplicate values)
        Assert.That(childValues, Has.Length.EqualTo(6));
    }

    // ─── GetOffspring ─────────────────────────────────────────────────────────

    [Test]
    public void GetOffspring_ForRecipeNodes_ReturnsIngredientNodes()
    {
        var data = CreateData();

        var offspringCount = data.Tree.Roots.GetOffspring().Count();

        // All non-root nodes: 21 total - 3 recipe roots = 18
        Assert.That(offspringCount, Is.EqualTo(18));
    }

    // ─── WithOffspring ────────────────────────────────────────────────────────

    [Test]
    public void WithOffspring_ForPastaNode_IncludesPastaAndItsIngredients()
    {
        var data = CreateData();
        var pastaNodes = data.Tree.Where(n => n.Value == data.Pasta);

        var withOffspringValues = pastaNodes.WithOffspring().Select(n => n.Value).ToArray();

        // Pasta + Dough + Flour + Grain + Wheat + Water + Tomato + OliveOil = 8
        Assert.That(withOffspringValues, Is.EquivalentTo(new ICookbookItem[]
        {
            data.Pasta, data.Dough, data.Flour, data.Grain, data.Wheat, data.Water,
            data.Tomato, data.OliveOil
        }));
    }

    // ─── GetBrothers ──────────────────────────────────────────────────────────

    [Test]
    public void GetBrothers_ForRecipeNode_ReturnsOtherRecipes()
    {
        var data = CreateData();
        var pastaNode = data.Tree.Roots.First(n => n.Value == data.Pasta);

        // Pasta has no parent, so no brothers
        var brothers = pastaNode.GetBrothers().ToArray();

        Assert.That(brothers, Is.Empty);
    }

    [Test]
    public void GetBrothers_ForIngredientNode_ReturnsSiblingsInSameRecipe()
    {
        var data = CreateData();
        // Dough under Pasta: siblings are Tomato and OliveOil nodes under Pasta
        var doughUnderPasta = data.Tree.GetSelf(data.Dough).First(n => n.Parent!.Value == data.Pasta);

        var brotherValues = doughUnderPasta.GetBrothers().Select(n => n.Value).ToArray();

        Assert.That(brotherValues, Is.EquivalentTo(new[] { data.Tomato, data.OliveOil }));
    }

    // ─── OrderByHierarchy ─────────────────────────────────────────────────────

    [Test]
    public void OrderByHierarchy_WithKeySelector_EachRootPrecedesItsOwnOffspring()
    {
        var data = CreateData();

        // OrderByHierarchy visits: root, then ALL offspring of that root sorted by key, then next root, ...
        var ordered = data.Tree.OrderByHierarchy(n => n.Value.Name).ToArray();

        // Roots appear in alphabetical Name order: Pasta, Pizza, Salad
        var orderedRootValues = ordered.Where(n => data.Recipes.Contains(n.Value)).Select(n => n.Value.Name).ToArray();
        Assert.That(orderedRootValues, Is.Ordered);

        // Each recipe node appears before all of its own ingredient nodes
        foreach (var recipeNode in data.Tree.Roots)
        {
            int recipeIdx = Array.IndexOf(ordered, recipeNode);
            foreach (var ingredientNode in recipeNode.GetOffspring())
            {
                int ingredientIdx = Array.IndexOf(ordered, ingredientNode);
                Assert.That(ingredientIdx, Is.GreaterThan(recipeIdx), $"{ingredientNode.Value.Name} should come after {recipeNode.Value.Name}");
            }
        }
    }

    [Test]
    public void OrderByHierarchy_ContainsAllNodes()
    {
        var data = CreateData();

        var ordered = data.Tree.OrderByHierarchy().ToArray();

        Assert.That(ordered, Has.Length.EqualTo(data.Tree.Count));
    }

    // ─── ToTreeView ───────────────────────────────────────────────────────────

    [Test]
    public void ToTreeView_ContainsAllNodeValues()
    {
        var data = CreateData();

        var treeView = data.Tree.ToTreeView();

        Assert.That(treeView, Has.Count.EqualTo(data.Tree.Count));
    }

    [Test]
    public void ToTreeView_FirstValueIsFirstRecipeRoot()
    {
        var data = CreateData();

        var treeView = data.Tree.ToTreeView();

        // The first element is Pasta (first root in insertion order)
        Assert.That(treeView[0], Is.EqualTo(data.Pasta));
        // Each recipe value appears before the ingredient values under it
        int pastaIdx = treeView.IndexOf(data.Pasta);
        int pizzaIdx = treeView.IndexOf(data.Pizza);
        int saladIdx = treeView.IndexOf(data.Salad);
        Assert.That(pastaIdx, Is.LessThan(pizzaIdx));
        Assert.That(pizzaIdx, Is.LessThan(saladIdx));
    }

    // ─── ReverseTree ──────────────────────────────────────────────────────────

    [Test]
    public void ReverseTree_IngredientValuesBecome_RootValues()
    {
        var data = CreateData();

        var reverseTree = data.Tree.ReverseTree();

        // The leaves of the original tree (Wheat, Water, Tomato, OliveOil, Mozzarella, Lettuce)
        // become the roots of the reversed tree.
        var reverseRootValues = reverseTree.Roots.Select(n => n.Value).ToArray();
        Assert.That(reverseRootValues, Is.EquivalentTo(data.LeafIngredients));
    }

    [Test]
    public void ReverseTree_RecipeValuesAppearAsLeafNodes()
    {
        var data = CreateData();

        var reverseTree = data.Tree.ReverseTree();

        var recipeNodesInReverse = reverseTree.Where(n => data.Recipes.Contains(n.Value)).ToArray();
        Assert.That(recipeNodesInReverse, Is.Not.Empty);
        Assert.That(recipeNodesInReverse.All(n => !n.Children.Any()), Is.True);
    }

    [Test]
    public void ReverseTree_ContainsAllDistinctValues()
    {
        var data = CreateData();

        var reverseTree = data.Tree.ReverseTree();

        var originalDistinct = data.Tree.Select(n => n.Value).Distinct();
        var reverseDistinct = reverseTree.Select(n => n.Value).Distinct();
        Assert.That(reverseDistinct, Is.EquivalentTo(originalDistinct));
    }

    // ─── Sub-ingredient hierarchy (new 4-level tests) ─────────────────────────

    [Test]
    public void GetSelf_DoughAppearsUnderTwoRecipes()
    {
        var data = CreateData();

        // Dough is a direct ingredient of Pasta and Pizza → 2 tree nodes
        var doughNodes = data.Tree.GetSelf(data.Dough).ToArray();

        Assert.That(doughNodes, Has.Length.EqualTo(2));
        Assert.That(doughNodes.All(n => n.Value == data.Dough), Is.True);
    }

    [Test]
    public void GetChildren_ForDough_ContainsFlourAndWater()
    {
        var data = CreateData();
        // Dough appears twice; pick the one under Pasta (both have the same children values)
        var doughUnderPasta = data.Tree.GetSelf(data.Dough).First(n => n.Parent!.Value == data.Pasta);

        var childValues = doughUnderPasta.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(childValues, Is.EquivalentTo(new[] { data.Flour, data.Water }));
    }

    [Test]
    public void GetChildren_ForFlour_ContainsGrain()
    {
        var data = CreateData();
        var flourNode = data.Tree.GetSelf(data.Flour).First();

        var childValues = flourNode.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(childValues, Is.EquivalentTo(new[] { data.Grain }));
    }

    [Test]
    public void GetChildren_ForGrain_ContainsWheat()
    {
        var data = CreateData();
        var grainNode = data.Tree.GetSelf(data.Grain).First();

        var childValues = grainNode.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(childValues, Is.EquivalentTo(new[] { data.Wheat }));
    }

    [Test]
    public void NodeLevel_WheatIsAtLevelFour()
    {
        var data = CreateData();

        // Wheat is four levels below the recipe root: Recipe(0) → Dough(1) → Flour(2) → Grain(3) → Wheat(4)
        var wheatNodes = data.Tree.GetSelf(data.Wheat).ToArray();

        Assert.That(wheatNodes, Is.Not.Empty);
        Assert.That(wheatNodes.All(n => n.Level == 4), Is.True);
    }

    [Test]
    public void NodeLevel_IntermediateIngredients_HaveCorrectLevels()
    {
        var data = CreateData();

        var doughNodes = data.Tree.GetSelf(data.Dough).ToArray();
        var flourNodes = data.Tree.GetSelf(data.Flour).ToArray();
        var grainNodes = data.Tree.GetSelf(data.Grain).ToArray();

        Assert.That(doughNodes.All(n => n.Level == 1), Is.True, "Dough should be at level 1");
        Assert.That(flourNodes.All(n => n.Level == 2), Is.True, "Flour should be at level 2");
        Assert.That(grainNodes.All(n => n.Level == 3), Is.True, "Grain should be at level 3");
    }

    [Test]
    public void GetAncestors_ForWheatNode_ReturnsFullPath()
    {
        var data = CreateData();
        // Wheat under Pasta: Grain → Flour → Dough → Pasta
        var wheatUnderPasta = data.Tree.GetSelf(data.Wheat).First(n => n.GetRoot().Value == data.Pasta);

        var ancestorValues = wheatUnderPasta.GetAncestors().Select(n => n.Value).ToArray();

        Assert.That(ancestorValues, Is.EquivalentTo(new ICookbookItem[] { data.Grain, data.Flour, data.Dough, data.Pasta }));
    }

    [Test]
    public void GetParents_ForWheatNode_ReturnsDirectParentGrain()
    {
        var data = CreateData();
        var wheatNodes = data.Tree.GetSelf(data.Wheat);

        var parentValues = wheatNodes.GetParents().Select(n => n.Value).Distinct().ToArray();

        // Wheat's only parent (in every occurrence) is Grain
        Assert.That(parentValues, Is.EquivalentTo(new[] { data.Grain }));
    }

    [Test]
    public void GetBrothers_ForSubIngredientNode_ReturnsSiblingsUnderSameParent()
    {
        var data = CreateData();
        // Flour's sibling under a Dough node is Water
        var flourNode = data.Tree.GetSelf(data.Flour).First();

        var brotherValues = flourNode.GetBrothers().Select(n => n.Value).ToArray();

        Assert.That(brotherValues, Is.EquivalentTo(new[] { data.Water }));
    }

    [Test]
    public void GetOffspring_ForDoughNode_ReturnsAllSubIngredients()
    {
        var data = CreateData();
        var doughUnderPasta = data.Tree.GetSelf(data.Dough).First(n => n.Parent!.Value == data.Pasta);

        var offspringValues = doughUnderPasta.GetOffspring().Select(n => n.Value).ToArray();

        // Flour, Water, Grain, Wheat (all descendants of Dough)
        Assert.That(offspringValues, Is.EquivalentTo(new ICookbookItem[] { data.Flour, data.Water, data.Grain, data.Wheat }));
    }

    [Test]
    public void GetRoots_ForWheatNodes_ReturnsRecipesOnlyViaFullPath()
    {
        var data = CreateData();
        var wheatNodes = data.Tree.GetSelf(data.Wheat);

        var rootValues = wheatNodes.GetRoots().Select(n => n.Value).ToArray();

        // Wheat exists under Pasta and Pizza only (not Salad)
        Assert.That(rootValues, Is.EquivalentTo(new[] { data.Pasta, data.Pizza }));
    }
}
