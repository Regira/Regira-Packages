using System.Diagnostics;
using System.Text;
using Regira.IO.Storage.FileSystem;
using Regira.System.Projects.Models;
using Regira.System.Projects.Services;
using Regira.TreeList;

namespace TreeList.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CreationTests
{
    public class TestData<T>
    {
        public TreeList<T> Tree { get; set; } = null!;
        public T[] Roots { get; set; } = null!;
        public T[] Values { get; set; } = null!;
    }
    public class FsItem
    {
        public string Path { get; set; } = null!;
        public string ParentDirectory { get; set; } = null!;
    }

    // A private tree, not the repo's tests/ folder. These fixtures enumerate the directory twice — once to
    // build the TreeList, once to assert against disk — so pointing them at a live source tree made them
    // fail whenever another test project happened to write into bin/ or obj/ between the two reads. The
    // shape below is what the assertions need: nested directories, files at several depths, an empty leaf.
    private string _testDirectory = null!;

    // A synthetic project graph for the ProjectTree fixtures, plus a directory holding two copies of
    // it. These used to run against the live repository, which meant they scanned whatever sat below
    // the solution folder -- git worktrees under .claude/ put three more copies of every project there,
    // and the tree grew to tens of thousands of nodes. The duplicate copy is now deliberate and small.
    private string _projectDirectory = null!;
    private string _projectCopiesDirectory = null!;

    [OneTimeSetUp]
    public void CreateTestDirectory()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "regira-treelist-tests", Guid.NewGuid().ToString("n"));
        foreach (var relative in new[] { "a/a1", "a/a2/a21", "b", "c/c1" })
        {
            Directory.CreateDirectory(Path.Combine(_testDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
        }
        foreach (var relative in new[] { "root.txt", "a/a.txt", "a/a1/a1.txt", "a/a2/a21/deep.txt", "b/b.txt" })
        {
            File.WriteAllText(Path.Combine(_testDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), relative);
        }

        _projectDirectory = NewTempDirectory();
        CreateProjectGraph(_projectDirectory);

        _projectCopiesDirectory = NewTempDirectory();
        CreateProjectGraph(Path.Combine(_projectCopiesDirectory, "main"));
        CreateProjectGraph(Path.Combine(_projectCopiesDirectory, "copy"));
    }

    private static string NewTempDirectory()
        => Path.Combine(Path.GetTempPath(), "regira-treelist-tests", Guid.NewGuid().ToString("n"));

    [OneTimeTearDown]
    public void RemoveTestDirectory()
    {
        foreach (var directory in new[] { _testDirectory, _projectDirectory, _projectCopiesDirectory })
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public void Create_Directories_Tree()
    {
        var data = GetDirectoryData();
        var tree = data.Tree;
        var directories = data.Values;
        var roots = data.Roots;

        Assert.That(tree.Count, Is.EqualTo(directories.Length));
        Assert.That(tree.Roots.Select(n => n.Value), Is.EquivalentTo(roots));
        foreach (var node in tree)
        {
            if (node.Parent == null)
            {
                Assert.That(roots, Has.Member(node.Value));
            }
            else
            {
                Assert.That(Path.GetDirectoryName(node.Value), Is.EqualTo(node.Parent.Value));
            }

            var subDirectories = Directory.GetDirectories(node.Value);
            var childDirectories = node.Children.Select(n => n.Value).ToArray();
            Assert.That(childDirectories, Is.EquivalentTo(subDirectories));
        }
    }
    [Test]
    public void Offspring()
    {
        var data = GetDirectoryData();
        var tree = data.Tree;

        foreach (var root in tree.Roots)
        {
            var subDirectories = Directory.GetDirectories(root.Value, string.Empty, SearchOption.AllDirectories);
            var offspring = root.GetOffspring().Select(o => o.Value);
            Assert.That(offspring, Is.EquivalentTo(subDirectories));
        }
    }
    [Test]
    public void Ancestors()
    {
        var data = GetDirectoryData();
        var tree = data.Tree;
        var directories = data.Values;

        var lowestLevelChild = tree.OrderByDescending(n => n.Level)
            .ThenBy(n => n.Value)
            .First();
        var highestSegmentCountDirectory = directories
            .OrderByDescending(d => d.Split('\\').Length)
            .ThenBy(d => d)
            .First();
        Assert.That(lowestLevelChild.Value, Is.EqualTo(highestSegmentCountDirectory));

        var ancestors = lowestLevelChild.GetAncestors()
            .Select(a => a.Value)
            .ToArray();
        var parentDirectories = Path.GetDirectoryName(highestSegmentCountDirectory)!
            .Substring(_testDirectory.Length)
            .Trim('\\')
            .Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(new List<string>(), (r, s) =>
            {
                r.Add(Path.Combine(_testDirectory, r.LastOrDefault() ?? string.Empty, s));
                return r;
            });
        Assert.That(ancestors, Is.EquivalentTo(parentDirectories));
    }

    [Test]
    public void Create_Files_And_Directories_Tree()
    {
        var data = GetFsItemData();
        var tree = data.Tree;
        var roots = data.Roots;
        var items = data.Values;

        Assert.That(tree.Count, Is.EqualTo(items.Length));
        foreach (var node in tree)
        {
            if (node.Parent == null)
            {
                Assert.That(roots, Has.Member(node.Value));
            }
            else
            {
                Assert.That(node.Value.ParentDirectory, Is.EqualTo(node.Parent!.Value.Path));
            }

            // only directories can have children
            if (Directory.Exists(node.Value.Path))
            {
                var subPaths = Directory.GetDirectories(node.Value.Path).Concat(Directory.GetFiles(node.Value.Path)).ToArray();
                var childPaths = node.Children.Select(n => n.Value.Path).ToArray();
                Assert.That(childPaths, Is.EquivalentTo(subPaths));
            }
        }
    }

    [Test]
    public void OrderBy_Hierarchy()
    {
        var data = GetFsItemData();
        var tree = data.Tree;
        var items = data.Values;

        var sortedItems = items
            .OrderBy(x => x.Path)
            .ToArray();
        var treeItems = tree.Select(n => n.Value).ToArray();
        var sw = new Stopwatch();
        sw.Start();
        var sortedNodes = tree.OrderByHierarchy(n => n.Value.Path)
            .ToArray();
        sw.Stop();
        Debug.Print($"OrderBy_Hierarchy tree.OrderByHierarchy: {sw.ElapsedMilliseconds} ms");

        var sortedNodeValues = sortedNodes
            .Select(n => n.Value)
            .ToArray();

        Assert.That(treeItems, Is.Not.EqualTo(sortedItems).AsCollection);
        Assert.That(sortedNodeValues, Is.EqualTo(sortedItems).AsCollection);
        // slow
        Assert.That(treeItems, Is.EquivalentTo(sortedItems));
    }

    static Task<ProjectTree> BuildProjectTree(string root)
        => new ProjectManager(new ProjectService(new ProjectParser(),
            new TextFileService(new FileSystemOptions { RootFolder = root }))).BuildTree();

    /// <summary>
    /// Writes a small class-library graph: Core depends on nothing, Data and Web depend on Core, and App
    /// depends on both. Each project sits in its own folder, so the ProjectReferences are relative paths
    /// the way they are in a real solution.
    /// </summary>
    static void CreateProjectGraph(string root)
    {
        var graph = new Dictionary<string, string[]>
        {
            ["Core"] = [],
            ["Data"] = ["Core"],
            ["Web"] = ["Core"],
            ["App"] = ["Data", "Web"]
        };

        foreach (var (name, dependencies) in graph)
        {
            var folder = Path.Combine(root, name);
            Directory.CreateDirectory(folder);

            var xml = new StringBuilder();
            xml.AppendLine("<Project Sdk='Microsoft.NET.Sdk'>");
            xml.AppendLine("  <PropertyGroup>");
            xml.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
            xml.AppendLine("  </PropertyGroup>");
            xml.AppendLine("  <ItemGroup>");
            foreach (var dependency in dependencies)
            {
                var reference = Path.Combine("..", dependency, dependency + ".csproj");
                xml.AppendLine($"    <ProjectReference Include='{reference}' />");
            }
            xml.AppendLine("  </ItemGroup>");
            xml.AppendLine("</Project>");

            File.WriteAllText(Path.Combine(folder, name + ".csproj"), xml.ToString());
        }
    }

    [Test]
    public async Task ReverseTree()
    {
        var tree = await BuildProjectTree(_projectDirectory);
        var reverseTree = tree.ReverseTree();
        // print tree
        Debug.Print("TREE");
        foreach (var node in tree)
        {
            Debug.Print($"{"".PadLeft(node.Level * 2, '.')}{node.Value.Title}");
        }
        Debug.Print("REVERSE");
        foreach (var node in reverseTree)
        {
            Debug.Print($"{"".PadLeft(node.Level * 2, '.')}{node.Value.Title}");
        }
        Assert.That(tree, Is.Not.EqualTo(reverseTree));
        Assert.That(tree, Is.Not.EquivalentTo(reverseTree));
        Assert.That(tree.Select(n => n.Value).Distinct(), Is.EquivalentTo(reverseTree.Select(n => n.Value).Distinct()));
        var treeRootValues = tree.Roots.Select(n => n.Value).Distinct();
        // an isolated project (no dependencies, no dependants) is a root in BOTH trees — there is no edge
        // to flip, so only roots with children gain a parent on reversal
        var invertibleRootValues = tree.Roots.Where(n => n.Children.Any()).Select(n => n.Value).Distinct().ToArray();
        Assert.That(invertibleRootValues, Is.Not.Empty); // guard: an empty set would make the next assert vacuously true
        var reverseTreeNodes = reverseTree.GetSelf(invertibleRootValues);
        Assert.That(reverseTreeNodes.All(n => n.Parent != null), Is.True);
        var reverseTreeBottomNodes = reverseTree.Where(n => !n.Children.Any()).Select(n => n.Value).Distinct();
        Assert.That(reverseTreeBottomNodes, Is.EquivalentTo(treeRootValues));
    }

    [Test]
    public async Task ProjectTree_KeepsIdenticalProjectCopiesApart()
    {
        var single = await BuildProjectTree(_projectDirectory);
        var copies = await BuildProjectTree(_projectCopiesDirectory);

        // Two copies of one graph below a single scan root are two graphs, not one. Matching a
        // ProjectReference on its relative suffix rather than its resolved path linked every copy to
        // every other, so the paths enumerated here grew combinatorially with the number of copies.
        Assert.That(copies.Count(), Is.EqualTo(single.Count() * 2));
    }

    [Test]
    public void TreeView()
    {
        var data = GetFsItemData();
        var tree = data.Tree;

        var sortedValues = tree.OrderByHierarchy()
            .Select(n => n.Value)
            .ToArray();
        var treeView = tree.ToTreeView();
        Assert.That(treeView, Is.EqualTo(sortedValues).AsCollection);
    }

    private TestData<string> GetDirectoryData()
    {
        var directories = Directory.GetDirectories(_testDirectory, string.Empty, SearchOption.AllDirectories)
            // shuffle
            .OrderBy(_ => Guid.NewGuid())
            .ToArray();
        var roots = directories.Where(r => !directories.Contains(Path.GetDirectoryName(r)))
            .ToArray();
        var tree = new TreeList<string>(directories.Length);
        tree.Fill(roots, parentNode => directories.Where(path => Path.GetDirectoryName(path) == parentNode.Value));
        return new TestData<string>
        {
            Tree = tree,
            Values = directories,
            Roots = roots
        };
    }
    private TestData<FsItem> GetFsItemData()
    {
        var directories = Directory.GetDirectories(_testDirectory, string.Empty, SearchOption.AllDirectories)
            .Select(d => new FsItem { Path = d, ParentDirectory = Path.GetDirectoryName(d)! });
        var files = Directory.GetFiles(_testDirectory, string.Empty, SearchOption.AllDirectories)
            .Select(f => new FsItem { Path = f, ParentDirectory = Path.GetDirectoryName(f)! });
        var items = directories.Concat(files)
            // Reverse of the order the tests sort back into, so the input is guaranteed not to be
            // path-sorted already. A random shuffle also has to satisfy that, but on a fixture this
            // small it lands sorted often enough to fail the precondition on its own.
            .OrderByDescending(x => x.Path)
            .ToArray();

        var sw = new Stopwatch();
        sw.Start();
        var roots = items.Where(x => x.ParentDirectory == _testDirectory.TrimEnd('\\'))
            .ToArray();
        var tree = new TreeList<FsItem>(items.Length);
        // using FsItem with already calculated ParentDirectory to prevent overhead of requesting Path.GetDirectoryName (up to 6 sec longer)
        tree.Fill(roots, parentNode => items.Where(item => item.ParentDirectory == parentNode.Value.Path));
        sw.Stop();
        Debug.Print($"GetFsItemData tree.Fill: {sw.ElapsedMilliseconds} ms");

        return new TestData<FsItem>
        {
            Tree = tree,
            Roots = roots,
            Values = items
        };
    }
}