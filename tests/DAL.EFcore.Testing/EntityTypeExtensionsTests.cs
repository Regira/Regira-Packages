using Microsoft.EntityFrameworkCore;
using Regira.DAL.EFcore.Extensions;
using Testing.Library.Data;

namespace DAL.EFcore.Testing;

[TestFixture]
public class EntityTypeExtensionsTests
{
    /// <summary>
    /// The attribute metadata cache is filled on first use, so concurrent requests that touch an entity type
    /// for the first time all reach the fill path at once. It used to be primed with a ContainsKey check
    /// followed by Add, and the caller that lost that race was handed
    /// "The key already existed in the dictionary" out of a save.
    /// </summary>
    [Test]
    public void GetPropertyAttributes_Survives_Concurrent_First_Use()
    {
        var dbContext = new ContosoContext(
            new DbContextOptionsBuilder<ContosoContext>().UseSqlite("Filename=:memory:").Options
        );
        var entityTypes = dbContext.Model.GetEntityTypes().ToArray();
        Assert.That(entityTypes, Is.Not.Empty);

        // Every worker races for the same entity types, so they collide on the same cache entries.
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();
        Assert.DoesNotThrow(() => Parallel.For(0, 64, _ =>
        {
            foreach (var entityType in entityTypes)
            {
                results.Add(entityType.GetPropertyAttributes().Count);
            }
        }));

        // And the cache still answers with the same content it would have without the contention.
        foreach (var entityType in entityTypes)
        {
            var expected = entityType.GetProperties().Count(p => p.PropertyInfo != null);
            Assert.That(entityType.GetPropertyAttributes(), Has.Count.EqualTo(expected));
        }
    }
}
