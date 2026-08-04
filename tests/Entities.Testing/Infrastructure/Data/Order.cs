using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Data;

public class Order : IEntityWithSerial
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime? OrderDate { get; set; }

    public ICollection<ProductOrder>? OrderedProducts { get; set; }
}