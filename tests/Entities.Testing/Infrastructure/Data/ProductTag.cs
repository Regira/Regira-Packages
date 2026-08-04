using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Data;

public class ProductTag : IEntityWithSerial
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? Value { get; set; }
    /// <summary>A marker field set by a test prepper to verify recursive invocation.</summary>
    public string? ProcessedValue { get; set; }
}
