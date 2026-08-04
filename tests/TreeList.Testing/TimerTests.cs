using Bogus;
using Regira.TreeList;
using Regira.Utilities;
using TreeList.Testing.Infrastructure;
using Person = TreeList.Testing.Infrastructure.Person;

namespace TreeList.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TimerTests
{
    public class TimerResult<T>
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Avg { get; set; }
        public TreeList<T> Tree { get; set; } = null!;
    }

    private readonly Randomizer _randomizer = new();

    [SetUp]
    public void SetUp()
    {
    }

    [Test]
    public void Simple_Create()
    {
        var persons = GenerateSimple(1000);

        var addByParentsSelectorAvg = TimeFillByGetParent(persons, 5);
        var addByChildrenSelectorAvg = TimeFillByGetChildren(persons, 5);
        var addSelfAvg = TimeAddSelf(persons, 5);

        Assert.That(addByParentsSelectorAvg.Tree.Select(n => n.Value), Is.EquivalentTo(addSelfAvg.Tree.Select(n => n.Value)));
        Assert.That(addByChildrenSelectorAvg.Tree.Select(n => n.Value), Is.EquivalentTo(addByParentsSelectorAvg.Tree.Select(n => n.Value)));
    }
    [Test]
    public void Simple()
    {
        var persons = GenerateSimple(1000);
        var addSelfAvg = TimeAddSelf(persons);
        foreach (var node in addSelfAvg.Tree)
        {
            var listParents = persons.Where(p => p.Children.Contains(node.Value)).ToArray();
            var treeParent = node.Parent?.Value;
            Assert.That(!listParents.Any() && treeParent == null || listParents.Contains(treeParent), Is.True);
            var listChildren = persons.Where(p => p.Parent == node.Value).ToArray();
            var treeChildren = node.Children.Select(c => c.Value).ToArray();
            Assert.That(treeChildren, Is.EquivalentTo(listChildren));
        }
    }
    [Test]
    public void Simple_HierarchySort()
    {
        var persons = GenerateSimple(10000);
        var addSelfAvg = TimeAddSelf(persons);
        var shuffledNodes = addSelfAvg.Tree
            .OrderBy(_ => Guid.NewGuid())
            .ToArray();
        var sortedNodes = shuffledNodes
            .OrderByHierarchy()
            .ToArray();

        TreeNode<SimplePerson> prevNode = null!;
        foreach (var sortedNode in sortedNodes)
        {
            Assert.That(sortedNode.Parent == null || sortedNode.Parent == prevNode || sortedNode.Parent == prevNode?.Parent
                          || sortedNode.GetAncestors().Intersect(prevNode!.GetAncestors()).Any(), Is.True);
            prevNode = sortedNode;
        }
    }
    [Test]
    public void Create_Relations()
    {
        var persons = GenerateAdvanced(1000);
        var addSelfAvg = TimeAddSelf(persons, 5);
        foreach (var node in addSelfAvg.Tree)
        {
            var listParents = persons.Where(p => p.Contacts.Any(c => c.Contact == node.Value)).ToArray();
            var treeParent = node.Parent?.Value;
            Assert.That(!listParents.Any() && treeParent == null || listParents.Contains(treeParent), Is.True);
        }
    }


    public TimerResult<SimplePerson> TimeFillByGetChildren(IEnumerable<SimplePerson> persons, int n = 1)
    {
        var results = Enumerable.Range(0, n)
            .Select(_ =>
            {
                var start = DateTime.Now;
                //var tree = persons.ToTreeList(x => x.Parent);
                var personList = persons.AsList();
                var tree = personList.ToTreeList(personList.FindAll(p => p.Parent == null), node => personList.Where(p => p.Parent == node.Value));
                Assert.That(tree, Is.Not.Empty);
                return new { duration = (DateTime.Now - start).TotalMilliseconds, tree };
            })
            .ToList();
        return new TimerResult<SimplePerson>
        {
            Min = results.Min(x => x.duration),
            Max = results.Max(x => x.duration),
            Avg = results.Average(x => x.duration),
            Tree = results.First().tree
        };
    }
    public TimerResult<SimplePerson> TimeFillByGetParent(IEnumerable<SimplePerson> persons, int n = 1)
    {
        var results = Enumerable.Range(0, n)
            .Select(_ =>
            {
                var start = DateTime.Now;
                var tree = new TreeList<SimplePerson>();
                tree.Fill(persons, x => new[] { x.Parent }.Where(p => p != null)!);
                Assert.That(tree, Is.Not.Empty);
                return new { duration = (DateTime.Now - start).TotalMilliseconds, tree };
            })
            .ToList();
        return new TimerResult<SimplePerson>
        {
            Min = results.Min(x => x.duration),
            Max = results.Max(x => x.duration),
            Avg = results.Average(x => x.duration),
            Tree = results.First().tree
        };
    }
    public TimerResult<SimplePerson> TimeAddSelf(IEnumerable<SimplePerson> persons, int n = 1)
    {
        void AddChildren(TreeNode<SimplePerson> node, List<SimplePerson> items)
        {
            var children = items.FindAll(x => x.Parent == node.Value);
            foreach (var child in children)
            {
                if (node.Value != child && node.GetAncestors().All(a => a.Value != child))
                {
                    var childNode = node.AddChild(child);
                    AddChildren(childNode, items);
                }
            }
        }

        var results = Enumerable.Range(0, n)
            .Select(_ =>
            {
                var start = DateTime.Now;
                var tree = new TreeList<SimplePerson>();
                var personList = persons.AsList();
                var roots = personList.FindAll(p => p.Parent == null);

                if (roots.Any())
                {
                    foreach (var person in roots)
                    {
                        var root = tree.AddValue(person);
                        if (root != null)
                        {
                            AddChildren(root, personList);
                        }
                    }

                    Assert.That(tree, Is.Not.Empty);
                }

                return new
                {
                    duration = (DateTime.Now - start).TotalMilliseconds,
                    tree
                };
            })
            .ToList();
        return new TimerResult<SimplePerson>
        {
            Min = results.Min(x => x.duration),
            Max = results.Max(x => x.duration),
            Avg = results.Average(x => x.duration),
            Tree = results.First().tree
        };
    }
    public TimerResult<Person> TimeAddSelf(IEnumerable<Person> persons, int n = 1)
    {
        var personList = persons.AsList();
        var contacts = personList.SelectMany(p => p.Contacts).ToList();
        void AddChildren(TreeNode<Person> node)
        {
            var children = node.Value.Contacts.Select(c => c.Contact);
            foreach (var child in children)
            {
                if (node.Value != child && node.GetAncestors().All(a => a.Value != child))
                {
                    var childNode = node.AddChild(child);
                    AddChildren(childNode);
                }
            }
        }

        var results = Enumerable.Range(0, n)
            .Select(_ =>
            {
                var start = DateTime.Now;
                var tree = new TreeList<Person>();
                var roots = personList.Where(p => contacts.All(c => c.Contact != p));


                foreach (var person in roots)
                {
                    var root = tree.AddValue(person);
                    if (root != null)
                    {
                        AddChildren(root);
                    }
                }

                Assert.That(tree, Is.Not.Empty);
                return new
                {
                    duration = (DateTime.Now - start).TotalMilliseconds,
                    tree
                };
            })
            .ToList();
        return new TimerResult<Person>
        {
            Min = results.Min(x => x.duration),
            Max = results.Max(x => x.duration),
            Avg = results.Average(x => x.duration),
            Tree = results.First().tree
        };
    }

    public List<SimplePerson> GenerateSimple(int n)
    {
        var persons = DataGenerator.GenerateSimplePersons(n);

        foreach (var person in persons)
        {
            if (_randomizer.Number(0, 9) < 9)
            {
                person.Parent = _randomizer.CollectionItem(persons.FindAll(p => p != person));
            }
        }
        foreach (var person in persons)
        {
            person.Children = persons.FindAll(p => p.Parent == person);
        }

        return persons;
    }
    public List<Person> GenerateAdvanced(int n)
    {
        var relationNames = new[] { "Father", "Mother", "Friend" };
        var persons = new Faker<Person>()
            .RuleFor(x => x.Id, (f, _) => f.IndexGlobal + 1)
            .RuleFor(x => x.GivenName, (f, _) => f.Name.FirstName())
            .RuleFor(x => x.FamilyName, (f, _) => f.Name.LastName())
            .Generate(n);
        var contacts = new Faker<Relation>()
            .RuleFor(x => x.RelationName, (f, _) => f.PickRandom(relationNames))
            .RuleFor(x => x.Person, (f, _) => f.PickRandom(persons))
            .RuleFor(x => x.Contact, (f, _) => f.PickRandom(persons))
            .Generate(n)
            .FindAll(c => c.Person != c.Contact);
        foreach (var contact in contacts)
        {
            contact.Person.Contacts = contacts.Where(c => c.Person == contact.Person).ToList();
        }

        return persons;
    }
}