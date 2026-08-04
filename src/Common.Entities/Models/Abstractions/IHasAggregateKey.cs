namespace Regira.Entities.Models.Abstractions;

public interface IHasAggregateKey
{
    Guid? AggregateKey { get; set; }
}