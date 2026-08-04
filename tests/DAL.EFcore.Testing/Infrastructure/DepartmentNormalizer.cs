//using Microsoft.EntityFrameworkCore;
//using Regira.Normalizing;
//using Regira.Normalizing.Abstractions;
//using Testing.Library.Contoso;
//using Testing.Library.Data;

//namespace DAL.EFcore.Testing.Infrastructure;

//public class DepartmentNormalizer(ContosoContext dbContext, IEntityNormalizer<IHasCourses> coursesNormalizer) : EntityNormalizer<Department>
//{
//    public override bool IsExclusive => true;
//    public override async Task HandleNormalizeMany(IEnumerable<Department?> items, bool recursive = false)
//    {
//        await base.HandleNormalizeMany(items, recursive);
//        await coursesNormalizer.HandleNormalizeMany(items);

//        var adminIds = items.Select(x => x?.AdministratorId).Where(id => id.HasValue).Distinct().ToArray();
//        var admins = await dbContext.Persons.Where(x => adminIds.Contains(x.Id)).ToListAsync();
//        foreach (var item in items)
//        {

//            var admin = admins.FirstOrDefault(x => x.Id == item?.AdministratorId);
//            if (admin != null)
//            {
//                item!.NormalizedContent = $"{item.NormalizedContent} {admin.NormalizedTitle}";
//            }
//        }
//    }
//}
