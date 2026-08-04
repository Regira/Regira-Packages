using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Data;

public class ProductOrder : IEntityWithSerial
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int OrderId { get; set; }
    public decimal Quantity { get; set; }

    public Product? Product { get; set; }
}