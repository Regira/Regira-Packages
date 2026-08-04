namespace Regira.Entities.Models.Abstractions;

public interface IHasObjectId<TKey>
{
    TKey ObjectId { get; set; }
}