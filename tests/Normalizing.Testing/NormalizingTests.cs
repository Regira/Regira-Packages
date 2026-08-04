using Normalizing.Testing.Services;
using Regira.Normalizing;
using Regira.Normalizing.Models;
using Testing.Library.Contoso;

namespace Normalizing.Testing;

[TestFixture]
public class NormalizingTests
{

    [Test]
    public Task Test_Default_Normalizer()
    {
        var item = new Person
        {
            GivenName = "John",
            LastName = "Doe"
        };

        NormalizingUtility.InvokeObjectNormalizer(item, new NormalizingOptions { Transform = TextTransform.ToUpperCase });

        Assert.That(item.NormalizedGivenName, Is.EqualTo(item.GivenName.ToUpperInvariant()));
        Assert.That(item.NormalizedLastName, Is.EqualTo(item.LastName.ToUpperInvariant()));

        return Task.CompletedTask;
    }

    [Test]
    public void Test_Custom_ObjectNormalizers()
    {
        var item = new Person
        {
            GivenName = "GlüH",
            LastName = "Wèîn",
            Phone = "+32 (0)899.99.99.99 (test)",
            Email = "Gluh.Wein@wines.com"
        };

        var normalizer = new DefaultNormalizer();
        var peopleNormalizer = new PeopleNormalizer(normalizer);

        NormalizingUtility.InvokeObjectNormalizer(item, new NormalizingOptions { DefaultObjectNormalizer = peopleNormalizer });

        Assert.That(item.NormalizedGivenName, Is.EqualTo(normalizer.Normalize(item.GivenName)));
        Assert.That(item.NormalizedLastName, Is.EqualTo(normalizer.Normalize(item.LastName)));
        Assert.That(item.NormalizedPhone, Is.EqualTo("+32 (0)899.99.99"));
        Assert.That(item.Email, Is.EqualTo(item.Email.ToLowerInvariant()));
    }

    [Test]
    public Task Recursive_Default_True()
    {
        var item = new Person
        {
            GivenName = "John",
            LastName = "Doe"
        };
        var supervisor = new Person
        {
            GivenName = "BÎG",
            LastName = "bôss",
            Subordinates = [item]
        };
        item.Supervisor = supervisor;

        NormalizingUtility.InvokeObjectNormalizer(item);

        Assert.That(item.Supervisor.NormalizedGivenName, Is.EqualTo("BIG"));
        Assert.That(item.Supervisor.NormalizedLastName, Is.EqualTo("boss"));

        return Task.CompletedTask;
    }
}