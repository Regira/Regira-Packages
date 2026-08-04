namespace Regira.Entities.Models.Abstractions;

public interface IHasParentEntity
{
    int? ParentEntityId { get; set; }
}
public interface IHasParentEntity<T> : IHasParentEntity
    where T : class, IEntity<int>
{
    T? ParentEntity { get; set; }
}