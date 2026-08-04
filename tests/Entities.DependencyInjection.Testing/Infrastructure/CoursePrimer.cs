using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Extensions;
using Testing.Library.Contoso;

namespace Entities.DependencyInjection.Testing.Infrastructure;

public class CoursePrimer : EntityPrimerBase<Course>
{
    public override Task PrepareAsync(Course entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entity.IsNew())
        {
            entity.Credits = 5;
        }

        return Task.CompletedTask;
    }
}