using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Normalizing.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing.Infrastructure.Normalizers;

public class InstructorNormalizer(IServiceProvider sp, INormalizer? normalizer) : DefaultEntityNormalizer<Instructor>(normalizer)
{
    public override async Task HandleNormalizeMany(IEnumerable<Instructor> items, CancellationToken token = default)
    {
        // injecting DbContext directly will create circular dependency, so use a IServiceProvider
        var dbContext = sp.GetRequiredService<ContosoContext>();

        var itemList = items.ToArray();
        var pendingCourses = dbContext.GetPendingEntries<Course>()
            .Select(e => e.Entity)
            .ToArray();
        var courseIds = itemList.SelectMany(x => x.Courses?.Select(d => d.Id) ?? [])
            .Except(pendingCourses.Select(d => d.Id));
        var courses = pendingCourses
            .Concat(await dbContext.Courses
                .Where(d => courseIds.Contains(d.Id))
                .AsNoTracking()
                .ToListAsync()
            )
            .ToArray();

        foreach (var item in itemList)
        {
            var itemCourses = courses.Where(d => item.Courses?.Any(x => x.Id == d.Id) == true).ToArray();

            await DefaultObjectNormalizer.HandleNormalizeMany(itemCourses);
            item.NormalizedContent = $"{item.NormalizedContent} INSTRUCTING {itemCourses.Length} COURSES";
        }
    }
}