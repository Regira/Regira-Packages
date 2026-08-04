using Microsoft.EntityFrameworkCore;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Normalizing.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing.Infrastructure.Normalizers;

public class DepartmentAdministratorNormalizer(ContosoContext dbContext, IEntityNormalizer<IHasCourses> hasCoursesNormalizer, INormalizer? normalizer)
    : EntityNormalizerBase<Department>(normalizer)
{
    public override bool IsExclusive => true;

    public override async Task HandleNormalizeMany(IEnumerable<Department> items, CancellationToken token = default)
    {
        var itemsList = items.ToArray();
        await hasCoursesNormalizer.HandleNormalizeMany(itemsList, token);

        var adminIds = itemsList.Select(x => x.AdministratorId).Where(id => id.HasValue).Distinct().ToArray();
        var admins = await dbContext.Persons.Where(x => adminIds.Contains(x.Id)).ToListAsync();
        foreach (var item in itemsList)
        {

            var admin = admins.FirstOrDefault(x => x.Id == item.AdministratorId);
            if (admin != null)
            {
                item.NormalizedContent = $"{item.NormalizedContent} {admin.NormalizedTitle}";
            }
        }
    }
}
