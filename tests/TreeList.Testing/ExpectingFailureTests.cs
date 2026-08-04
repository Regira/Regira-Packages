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

        var tree = persons.ToTreeList(p => p.Parent!);
        Assert.That(tree, Is.Empty);
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