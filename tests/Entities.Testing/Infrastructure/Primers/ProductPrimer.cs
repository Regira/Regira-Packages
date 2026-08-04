using Entities.Testing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;

namespace Entities.Testing.Infrastructure.Primers;

public class ProductPrimer : EntityPrimerBase<Product>
{
    public const string DescriptionMessage = "Entity is prepared";

    public override Task PrepareAsync(Product entity, EntityEntry _, CancellationToken token = default)
    {
        entity.Description = DescriptionMessage;

        return Task.CompletedTask;
    }
}