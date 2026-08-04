using Regira.TreeList;
using Regira.TreeList.Abstractions;
using TreeList.Testing.Infrastructure;

namespace TreeList.Testing;

/// <summary>
/// Unit tests for <see cref="TreeListExtensions"/> using a family tree scenario (one-to-many relationships).
/// <para>
/// Tree structure used across all tests:
/// <code>
/// Grandpa (1)
/// ├── Father (2)
/// │   ├── Child1 (4)
/// │   │   └── GrandChild1 (7)
/// │   └── Child2 (5)
/// └── Uncle (3)
///     └── Cousin (6)
///         └── GrandChild2 (8)
/// </code>
/// </para>
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class FamilyTreeTests
{
    private class FamilyTreeData
    {
        public TreeList<FamilyMember> Tree { get; init; } = null!;
        public FamilyMember Grandpa { get; init; } = null!;
        public FamilyMember Father { get; init; } = null!;
        public FamilyMember Uncle { get; init; } = null!;
        public FamilyMember Child1 { get; init; } = null!;
        public FamilyMember Child2 { get; init; } = null!;
        public FamilyMember Cousin { get; init; } = null!;
        public FamilyMember GrandChild1 { get; init; } = null!;
        public FamilyMember GrandChild2 { get; init; } = null!;
        public FamilyMember[] AllMembers { get; init; } = null!;
    }

    private static FamilyTreeData CreateData()
    {
        var grandpa = new FamilyMember { Id = 1, Name = "Grandpa" };
        var father = new FamilyMember { Id = 2, Name = "Father" };
        var uncle = new FamilyMember { Id = 3, Name = "Uncle" };
        var child1 = new FamilyMember { Id = 4, Name = "Child1" };
        var child2 = new FamilyMember { Id = 5, Name = "Child2" };
        var cousin = new FamilyMember { Id = 6, Name = "Cousin" };
        var grandChild1 = new FamilyMember { Id = 7, Name = "GrandChild1" };
        var grandChild2 = new FamilyMember { Id = 8, Name = "GrandChild2" };

        father.Parents = [grandpa];
        uncle.Parents = [grandpa];
        child1.Parents = [father];
        child2.Parents = [father];
        cousin.Parents = [uncle];
        grandChild1.Parents = [child1];
        grandChild2.Parents = [cousin];

        var allMembers = new[] { grandpa, father, uncle, child1, child2, cousin, grandChild1, grandChild2 };
        var tree = new TreeList<FamilyMember>();
        tree.Fill(allMembers, m => m.Parents ?? []);

        return new FamilyTreeData
        {
            Tree = tree,
            Grandpa = grandpa,
            Father = father,
            Uncle = uncle,
            Child1 = child1,
            Child2 = child2,
            Cousin = cousin,
            GrandChild1 = grandChild1,
            GrandChild2 = grandChild2,
            AllMembers = allMembers
        };
    }

    // ─── ToTreeList overloads ──────────────────────────────────────────────────

    [Test]
    public void Fill_WithGetParents_BuildsCorrectTree()
    {
        var data = CreateData();
        var tree = data.Tree;

        Assert.That(tree.Count, Is.EqualTo(data.AllMembers.Length));
        Assert.That(tree.Roots, Has.Length.EqualTo(1));
        Assert.That(tree.Roots[0].Value, Is.EqualTo(data.Grandpa));
    }

    [Test]
    public void ToTreeList_WithChildrenSelector_BuildsEquivalentTree()
    {
        var data = CreateData();
        var roots = new[] { data.Grandpa };
        var allMembers = data.AllMembers;

        var tree = allMembers.ToTreeList(
            roots,
            (ITreeNode<FamilyMember> node) => allMembers.Where(m => m.Parents?.Contains(node.Value) == true));

        Assert.That(tree.Count, Is.EqualTo(allMembers.Length));
        Assert.That(tree.Roots.Select(n => n.Value), Is.EquivalentTo(roots));
    }

    [Test]
    public void ToTreeList_WithParentCollection_BuildsEquivalentTree()
    {
        var data = CreateData();

        // Using fkSelector overload: for each member, find the members that have it as a child
        var allMembers = data.AllMembers;
        var tree = allMembers.ToTreeList(
            m => m.Parents ?? []);

        Assert.That(tree.Count, Is.EqualTo(data.AllMembers.Length));
        Assert.That(tree.Roots.Select(n => n.Value), Is.EquivalentTo(new[] { data.Grandpa }));
    }

    [Test]
    public void ToTreeList_FromNodes_PreservesCount()
    {
        var data = CreateData();

        // IEnumerable<TreeNode<T>>.ToTreeList()
        var newTree = data.Tree.ToTreeList();

        Assert.That(newTree.Count, Is.EqualTo(data.Tree.Count));
    }

    [Test]
    public void ToTreeList_FlatCollection_AllItemsAreRoots()
    {
        var data = CreateData();

        // ToTreeList(IEnumerable<T>) with no parent selector → all items become roots
        var flatTree = data.AllMembers.ToTreeList();

        Assert.That(flatTree.Count, Is.EqualTo(data.AllMembers.Length));
        Assert.That(flatTree.Roots, Has.Length.EqualTo(data.AllMembers.Length));
    }

    // ─── GetRoot ──────────────────────────────────────────────────────────────

    [Test]
    public void GetRoot_FromLeafNode_ReturnsGrandpa()
    {
        var data = CreateData();

        var root = data.Tree.First(n => n.Value == data.GrandChild1).GetRoot();

        Assert.That(root.Value, Is.EqualTo(data.Grandpa));
    }

    [Test]
    public void GetRoot_FromRootNode_ReturnsSelf()
    {
        var data = CreateData();

        var grandpaNode = data.Tree.First(n => n.Value == data.Grandpa);
        var root = grandpaNode.GetRoot();

        Assert.That(root, Is.SameAs(grandpaNode));
    }

    // ─── GetAncestors (single node) ───────────────────────────────────────────

    [Test]
    public void GetAncestors_ForGrandchild_ReturnsFullAncestorChain()
    {
        var data = CreateData();
        var grandChild1Node = data.Tree.First(n => n.Value == data.GrandChild1);

        var ancestors = grandChild1Node.GetAncestors().Select(n => n.Value).ToArray();

        Assert.That(ancestors, Is.EqualTo(new[] { data.Grandpa, data.Father, data.Child1 }));
    }

    [Test]
    public void GetAncestors_ForRootNode_IsEmpty()
    {
        var data = CreateData();
        var grandpaNode = data.Tree.First(n => n.Value == data.Grandpa);

        var ancestors = grandpaNode.GetAncestors().ToArray();

        Assert.That(ancestors, Is.Empty);
    }

    // ─── GetChildren (single node) ────────────────────────────────────────────

    [Test]
    public void GetChildren_ForFather_ReturnsTwoChildren()
    {
        var data = CreateData();
        var fatherNode = data.Tree.First(n => n.Value == data.Father);

        var children = fatherNode.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(children, Is.EquivalentTo(new[] { data.Child1, data.Child2 }));
    }

    [Test]
    public void GetChildren_ForLeafNode_IsEmpty()
    {
        var data = CreateData();
        var child2Node = data.Tree.First(n => n.Value == data.Child2);

        var children = child2Node.GetChildren().ToArray();

        Assert.That(children, Is.Empty);
    }

    // ─── GetOffspring (single node) ───────────────────────────────────────────

    [Test]
    public void GetOffspring_ForGrandpa_ContainsAllDescendants()
    {
        var data = CreateData();
        var grandpaNode = data.Tree.First(n => n.Value == data.Grandpa);

        var offspringValues = grandpaNode.GetOffspring().Select(n => n.Value).ToArray();
        var expectedDescendants = new[] { data.Father, data.Child1, data.GrandChild1, data.Child2, data.Uncle, data.Cousin, data.GrandChild2 };

        Assert.That(offspringValues, Is.EquivalentTo(expectedDescendants));
    }

    [Test]
    public void GetOffspring_ForFather_ContainsOnlyFathersDescendants()
    {
        var data = CreateData();
        var fatherNode = data.Tree.First(n => n.Value == data.Father);

        var offspringValues = fatherNode.GetOffspring().Select(n => n.Value).ToArray();

        Assert.That(offspringValues, Is.EquivalentTo(new[] { data.Child1, data.GrandChild1, data.Child2 }));
    }

    // ─── GetBrothers (single node) ────────────────────────────────────────────

    [Test]
    public void GetBrothers_ForFather_ReturnsUncle()
    {
        var data = CreateData();
        var fatherNode = data.Tree.First(n => n.Value == data.Father);

        var brothers = fatherNode.GetBrothers().Select(n => n.Value).ToArray();

        Assert.That(brothers, Is.EquivalentTo(new[] { data.Uncle }));
    }

    [Test]
    public void GetBrothers_ForChild1_ReturnsChild2()
    {
        var data = CreateData();
        var child1Node = data.Tree.First(n => n.Value == data.Child1);

        var brothers = child1Node.GetBrothers().Select(n => n.Value).ToArray();

        Assert.That(brothers, Is.EquivalentTo(new[] { data.Child2 }));
    }

    [Test]
    public void GetBrothers_ForRootNode_IsEmpty()
    {
        var data = CreateData();
        var grandpaNode = data.Tree.First(n => n.Value == data.Grandpa);

        var brothers = grandpaNode.GetBrothers().ToArray();

        Assert.That(brothers, Is.Empty);
    }

    [Test]
    public void GetBrothers_ForOnlyChild_IsEmpty()
    {
        var data = CreateData();
        var cousinNode = data.Tree.First(n => n.Value == data.Cousin);

        var brothers = cousinNode.GetBrothers().ToArray();

        Assert.That(brothers, Is.Empty);
    }

    // ─── GetUncles (single node) ──────────────────────────────────────────────

    [Test]
    public void GetUncles_ForChild1_ReturnsCousin()
    {
        // GetUncles = children of parent's siblings
        var data = CreateData();
        var child1Node = data.Tree.First(n => n.Value == data.Child1);

        var uncles = child1Node.GetUncles().Select(n => n.Value).ToArray();

        Assert.That(uncles, Is.EquivalentTo(new[] { data.Cousin }));
    }

    [Test]
    public void GetUncles_ForCousin_ReturnsChild1AndChild2()
    {
        var data = CreateData();
        var cousinNode = data.Tree.First(n => n.Value == data.Cousin);

        var uncles = cousinNode.GetUncles().Select(n => n.Value).ToArray();

        Assert.That(uncles, Is.EquivalentTo(new[] { data.Child1, data.Child2 }));
    }

    [Test]
    public void GetUncles_ForNodeWhoseParentHasNoSiblings_IsEmpty()
    {
        var data = CreateData();
        // Grandpa is the only root: Father's uncles = Grandpa's siblings' children = empty
        var fatherNode = data.Tree.First(n => n.Value == data.Father);

        var uncles = fatherNode.GetUncles().ToArray();

        Assert.That(uncles, Is.Empty);
    }

    // ─── GetNephews (single node) ─────────────────────────────────────────────

    [Test]
    public void GetNephews_ForChild1_ReturnsGrandChild2()
    {
        // GetNephews = children of GetUncles = children of Cousin = [GrandChild2]
        var data = CreateData();
        var child1Node = data.Tree.First(n => n.Value == data.Child1);

        var nephews = child1Node.GetNephews().Select(n => n.Value).ToArray();

        Assert.That(nephews, Is.EquivalentTo(new[] { data.GrandChild2 }));
    }

    [Test]
    public void GetNephews_ForCousin_ReturnsGrandChild1()
    {
        // GetNephews = children of GetUncles = children of [Child1, Child2] = [GrandChild1]
        var data = CreateData();
        var cousinNode = data.Tree.First(n => n.Value == data.Cousin);

        var nephews = cousinNode.GetNephews().Select(n => n.Value).ToArray();

        Assert.That(nephews, Is.EquivalentTo(new[] { data.GrandChild1 }));
    }

    // ─── GetSelf ──────────────────────────────────────────────────────────────

    [Test]
    public void GetSelf_SingleItem_ReturnsMatchingNode()
    {
        var data = CreateData();

        var fatherNodes = data.Tree.GetSelf(data.Father).ToArray();

        Assert.That(fatherNodes, Has.Length.EqualTo(1));
        Assert.That(fatherNodes[0].Value, Is.EqualTo(data.Father));
    }

    [Test]
    public void GetSelf_MultipleItems_ReturnsAllMatchingNodes()
    {
        var data = CreateData();

        var nodes = data.Tree.GetSelf(new[] { data.Father, data.Uncle }).ToArray();

        Assert.That(nodes.Select(n => n.Value), Is.EquivalentTo(new[] { data.Father, data.Uncle }));
    }

    // ─── GetParents (collection) ──────────────────────────────────────────────

    [Test]
    public void GetParents_ForChildNodes_ReturnsDistinctParents()
    {
        var data = CreateData();
        var childNodes = data.Tree.Where(n => n.Value == data.Child1 || n.Value == data.Child2 || n.Value == data.Cousin);

        var parents = childNodes.GetParents().Select(n => n.Value).ToArray();

        Assert.That(parents, Is.EquivalentTo(new[] { data.Father, data.Uncle }));
    }

    [Test]
    public void GetParents_ForRootNodes_IsEmpty()
    {
        var data = CreateData();

        var parents = data.Tree.Roots.GetParents().ToArray();

        Assert.That(parents, Is.Empty);
    }

    // ─── GetAncestors (collection) ────────────────────────────────────────────

    [Test]
    public void GetAncestors_ForCollection_ReturnsDistinctAncestors()
    {
        var data = CreateData();
        var nodes = data.Tree.Where(n => n.Value == data.Child1 || n.Value == data.GrandChild2);

        var ancestorValues = nodes.GetAncestors().Select(n => n.Value).ToArray();

        // Child1's ancestors: [Grandpa, Father]
        // GrandChild2's ancestors: [Grandpa, Uncle, Cousin]
        // Combined distinct:
        Assert.That(ancestorValues, Is.EquivalentTo(new[] { data.Grandpa, data.Father, data.Uncle, data.Cousin }));
    }

    // ─── GetRoots (collection) ────────────────────────────────────────────────

    [Test]
    public void GetRoots_ForAllNodes_ReturnsOnlyGrandpa()
    {
        var data = CreateData();

        var roots = data.Tree.GetRoots().Select(n => n.Value).ToArray();

        Assert.That(roots, Is.EquivalentTo(new[] { data.Grandpa }));
    }

    [Test]
    public void GetRoots_ForSubset_ReturnsCommonRoot()
    {
        var data = CreateData();
        var nodes = data.Tree.Where(n => n.Value == data.Child1 || n.Value == data.Cousin);

        var roots = nodes.GetRoots().Select(n => n.Value).ToArray();

        Assert.That(roots, Is.EquivalentTo(new[] { data.Grandpa }));
    }

    // ─── GetBottom (collection) ───────────────────────────────────────────────

    [Test]
    public void GetBottom_ForTree_ReturnsLeafNodes()
    {
        var data = CreateData();

        var bottomValues = data.Tree.GetBottom().Select(n => n.Value).ToArray();

        Assert.That(bottomValues, Is.EquivalentTo(new[] { data.Child2, data.GrandChild1, data.GrandChild2 }));
    }

    // ─── GetChildren (collection) ─────────────────────────────────────────────

    [Test]
    public void GetChildren_ForCollection_ReturnsAllDirectChildren()
    {
        var data = CreateData();
        var topLevelNodes = data.Tree.Where(n => n.Value == data.Father || n.Value == data.Uncle);

        var childValues = topLevelNodes.GetChildren().Select(n => n.Value).ToArray();

        Assert.That(childValues, Is.EquivalentTo(new[] { data.Child1, data.Child2, data.Cousin }));
    }

    // ─── GetOffspring (collection) ────────────────────────────────────────────

    [Test]
    public void GetOffspring_ForRoots_ReturnsAllDescendants()
    {
        var data = CreateData();

        var offspringValues = data.Tree.Roots.GetOffspring().Select(n => n.Value).ToArray();
        var expectedDescendants = new[] { data.Father, data.Child1, data.GrandChild1, data.Child2, data.Uncle, data.Cousin, data.GrandChild2 };

        Assert.That(offspringValues, Is.EquivalentTo(expectedDescendants));
    }

    // ─── WithOffspring ────────────────────────────────────────────────────────

    [Test]
    public void WithOffspring_IncludesSelfAndAllDescendants()
    {
        var data = CreateData();
        var fatherNode = data.Tree.Where(n => n.Value == data.Father);

        var withOffspringValues = fatherNode.WithOffspring().Select(n => n.Value).ToArray();

        Assert.That(withOffspringValues, Is.EquivalentTo(new[] { data.Father, data.Child1, data.GrandChild1, data.Child2 }));
    }

    // ─── GetBrothers (collection) ─────────────────────────────────────────────

    [Test]
    public void GetBrothers_ForCollection_ReturnsDistinctSiblings()
    {
        var data = CreateData();
        var nodes = data.Tree.Where(n => n.Value == data.Father);

        var brotherValues = nodes.GetBrothers().Select(n => n.Value).ToArray();

        Assert.That(brotherValues, Is.EquivalentTo(new[] { data.Uncle }));
    }

    // ─── GetUncles (collection) ───────────────────────────────────────────────

    [Test]
    public void GetUncles_ForCollection_ReturnsDistinctUncles()
    {
        var data = CreateData();
        // Child1's uncles: [Cousin]; Cousin's uncles: [Child1, Child2]
        var nodes = data.Tree.Where(n => n.Value == data.Child1 || n.Value == data.Cousin);

        var uncleValues = nodes.GetUncles().Select(n => n.Value).ToArray();

        Assert.That(uncleValues, Is.EquivalentTo(new[] { data.Cousin, data.Child1, data.Child2 }));
    }

    // ─── OrderByHierarchy ─────────────────────────────────────────────────────

    [Test]
    public void OrderByHierarchy_WithoutKeySelector_IsDepthFirst()
    {
        var data = CreateData();

        var orderedValues = data.Tree.OrderByHierarchy().Select(n => n.Value).ToArray();

        // Depth-first: root first, then offspring in traversal order
        var expectedOrder = new[] { data.Grandpa, data.Father, data.Child1, data.GrandChild1, data.Child2, data.Uncle, data.Cousin, data.GrandChild2 };
        Assert.That(orderedValues, Is.EqualTo(expectedOrder).AsCollection);
    }

    [Test]
    public void OrderByHierarchy_WithKeySelector_SortsOffspringByKey()
    {
        var data = CreateData();

        // Each root's full offspring are sorted by Id, so all descendants appear in Id order after their root
        var orderedValues = data.Tree.OrderByHierarchy(n => n.Value.Id).Select(n => n.Value).ToArray();

        // Root: Grandpa(1); then all offspring sorted by Id: Father(2), Uncle(3), Child1(4), Child2(5), Cousin(6), GrandChild1(7), GrandChild2(8)
        var expectedOrder = new[] { data.Grandpa, data.Father, data.Uncle, data.Child1, data.Child2, data.Cousin, data.GrandChild1, data.GrandChild2 };
        Assert.That(orderedValues, Is.EqualTo(expectedOrder).AsCollection);
    }

    // ─── ToTreeView ───────────────────────────────────────────────────────────

    [Test]
    public void ToTreeView_ReturnsValuesInHierarchicalOrder()
    {
        var data = CreateData();

        var treeView = data.Tree.ToTreeView();

        var expectedOrder = new[] { data.Grandpa, data.Father, data.Child1, data.GrandChild1, data.Child2, data.Uncle, data.Cousin, data.GrandChild2 };
        Assert.That(treeView, Is.EqualTo(expectedOrder).AsCollection);
    }

    [Test]
    public void ToTreeView_BackingTreeIsOriginalTree()
    {
        var data = CreateData();

        var treeView = data.Tree.ToTreeView();

        Assert.That(treeView.Tree, Is.SameAs(data.Tree));
    }

    // ─── ReverseTree ──────────────────────────────────────────────────────────

    [Test]
    public void ReverseTree_OriginalLeafNodesBecome_ReversedTreeRoots()
    {
        var data = CreateData();

        var reverseTree = data.Tree.ReverseTree();

        var reverseRootValues = reverseTree.Roots.Select(n => n.Value).ToArray();
        Assert.That(reverseRootValues, Is.EquivalentTo(new[] { data.Child2, data.GrandChild1, data.GrandChild2 }));
    }

    [Test]
    public void ReverseTree_OriginalRootAppearsAsLeafNode()
    {
        var data = CreateData();

        var reverseTree = data.Tree.ReverseTree();

        var grandpaNodesInReverseTree = reverseTree.Where(n => n.Value == data.Grandpa).ToArray();
        Assert.That(grandpaNodesInReverseTree, Is.Not.Empty);
        Assert.That(grandpaNodesInReverseTree.All(n => !n.Children.Any()), Is.True);
    }

    [Test]
    public void ReverseTree_ContainsAllDistinctValues()
    {
        var data = CreateData();

        var reverseTree = data.Tree.ReverseTree();

        var originalDistinctValues = data.Tree.Select(n => n.Value).Distinct();
        var reverseDistinctValues = reverseTree.Select(n => n.Value).Distinct();
        Assert.That(reverseDistinctValues, Is.EquivalentTo(originalDistinctValues));
    }
}
