namespace Regira.Entities.Models.Abstractions;

public interface IHasDefault
{
    bool IsDefault { get; set; }
}
public interface IHasDefault<TKey> : IEntity<TKey>, IHasDefault;