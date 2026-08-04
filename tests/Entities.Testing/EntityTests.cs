using Entities.Testing.Infrastructure.Data;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Extensions;
using Regira.Entities.Models.Abstractions;
using Testing.Library.Contoso;

namespace Entities.Testing;

[TestFixture]
public class EntityTests
{
    [Test]
    public void IsNew_For_Int()
    {
        var item = new Category();
        var isNew = item.IsNew();
        Assert.That(isNew, Is.True);
    }
    [Test]
    public void IsNew_For_String()
    {
        var item = new User();
        var isNew = item.IsNew();
        Assert.That(isNew, Is.True);
    }

    [Test]
    public void Not_IsNew_For_Int()
    {
        var item = new Category { Id = 1 };
        var isNew = item.IsNew();
        Assert.That(isNew, Is.False);
    }
    [Test]
    public void Not_IsNew_For_String()
    {
        var item = new User { Id = Guid.NewGuid().ToString("N") };
        var isNew = item.IsNew();
        Assert.That(isNew, Is.False);
    }

    [Test]
    public void Test_Types()
    {
        List<IEntity> items = [new Person(), new Person(), new Instructor(), new Course(), new Student(), new Instructor(), new Department()];
        var students = items.OfType<Student>();
        Assert.That(students.Count(), Is.EqualTo(1));
        var persons = items.OfType<Person>();
        Assert.That(persons.Count(), Is.EqualTo(5));
        var itemsWithAttachments = items.OfType<IHasAttachments>();
        Assert.That(itemsWithAttachments.Count(), Is.EqualTo(6));
        var itemsWithNormalizedTitle = items.OfType<IHasNormalizedTitle>();
        Assert.That(itemsWithNormalizedTitle.Count(), Is.EqualTo(7));
    }
}
