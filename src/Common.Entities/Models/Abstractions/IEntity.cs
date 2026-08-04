namespace Regira.Entities.Models.Abstractions;

public interface IEntity;

public interface IEntity<TKey> : IEntity
{
    public TKey Id { get; set; }
}