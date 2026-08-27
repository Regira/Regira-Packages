using Regira.TreeList;
using TreeList.Testing.Infrastructure;
using Person = TreeList.Testing.Infrastructure.Person;

namespace TreeList.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ExpectingFailureTests
{
    [Test]
    public void ToTreeList_With_Self_Referencing_Parent()
    {
        var persons = DataGenerator.GenerateSimplePersons(2);
        var person0 = persons[0];
        var person1 = persons[1];
        person1.Parent = person0;
        person0.Parent = person1;

        // Both persons have a parent, so neither is a root and neither can be reached while walking down
        Assert.Throws<InvalidChildException<SimplePerson>>(() => persons.ToTreeList(p => p.Parent!));
    }
    [Test]
    public void ToTreeList_With_Self_Referencing_Parent_Fails_Silently()
    {
        var persons = DataGenerator.GenerateSimplePersons(2);
        var person0 = persons[0];
        var person1 = persons[1];
        person1.Parent = person0;
        person0.Parent = person1;

        var tree = persons.ToTreeList(p => p.Parent!, new TreeList<SimplePerson>.TreeOptions { ThrowOnError = false });

        Assert.That(tree, Is.Empty);
    }
    [Test]
    public void ToTreeList_With_Unreachable_Cycle_Beside_A_Root()
    {
        var persons = DataGenerator.GenerateSimplePersons(3);
        var root = persons[0];
        var person1 = persons[1];
        var person2 = persons[2];
        person1.Parent = person2;
        person2.Parent = person1;

        var ex = Assert.Throws<InvalidChildException<SimplePerson>>(() => persons.ToTreeList(p => p.Parent!));

        Assert.That(ex!.Child, Is.EqualTo(person1));
        Assert.That(persons.ToTreeList(p => p.Parent!, new TreeList<SimplePerson>.TreeOptions { ThrowOnError = false }).Select(n => n.Value), Is.EqualTo(new[] { root }).AsCollection);
    }
    [Test]
    public void ToTreeList_With_Multiple_Parents_Does_Not_Report_Unreachable()
    {
        var members = new[]
        {
            new FamilyMember { Id = 1, Name = "Grandpa" },
            new FamilyMember { Id = 2, Name = "Grandma" },
            new FamilyMember { Id = 3, Name = "Child" }
        };
        members[2].Parents = [members[0], members[1]];

        // Child gets a node under each parent, so the tree holds more nodes than there are values
        var tree = members.ToTreeList(m => m.Parents ?? []);

        Assert.That(tree.Count, Is.EqualTo(4));
        Assert.That(tree.Select(n => n.Value).Distinct(), Is.EquivalentTo(members));
    }
    [Test]
    public void Fill_From_Children_Over_A_Cycle_Fails_Silently()
    {
        var persons = DataGenerator.GenerateSimplePersons(3);
        var person0 = persons[0];
        var person1 = persons[1];
        var person2 = persons[2];
        // A -> B -> C -> B: the cycle is reachable from the root, so the walk runs into it
        var children = new Dictionary<SimplePerson, SimplePerson[]>
        {
            [person0] = [person1],
            [person1] = [person2],
            [person2] = [person1]
        };

        var tree = persons.ToTreeList([person0], node => children[node.Value], new TreeList<SimplePerson>.TreeOptions { ThrowOnError = false });

        Assert.That(tree.Select(n => n.Value), Is.EqualTo(new[] { person0, person1, person2 }).AsCollection);
        Assert.Throws<InvalidChildException<SimplePerson>>(() => persons.ToTreeList([person0], node => children[node.Value]));
    }
    [Test]
    public void ToTreeList_With_Invalid_Relation()
    {
        var persons = DataGenerator.GeneratePersons(4);
        var person0 = persons[0];
        var person1 = persons[1];
        var person2 = persons[2];
        var person3 = persons[3];
        person0.Contacts.Add(new Relation { Contact = person1 });
        person1.Contacts.Add(new Relation { Contact = person2 });
        person2.Contacts.Add(new Relation { Contact = person3 });
        // recursive relation
        person3.Contacts.Add(new Relation { Contact = person1 });

        Assert.Throws<InvalidChildException<Person>>(() => persons.ToTreeList(p => persons.FindAll(c => c.Contacts.Any(pc => pc.Contact == p))));
    }
    [Test]
    public void Add_Invalid_Child_Fail_Silently()
    {
        var persons = DataGenerator.GenerateSimplePersons(3);
        var person0 = persons[0];
        var person1 = persons[1];
        var person2 = persons[2];

        var tree = new TreeList<SimplePerson>(new TreeList<SimplePerson>.TreeOptions
        {
            EnableAutoCheck = true,
            ThrowOnError = false
        });
        var node0 = tree.AddValue(person0);
        var node1 = tree.AddValue(person1, node0);
        var node2 = tree.AddValue(person2, node1);
        var invalidNode = tree.AddValue(person0, node2);

        Assert.That(invalidNode, Is.Null);
    }
}