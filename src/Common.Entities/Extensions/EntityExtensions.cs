using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Extensions;

public static class EntityExtensions
{
    public static bool IsNew<TKey>(this IEntity<TKey> item)
        => item.Id?.Equals(default(TKey)) ?? true;

    public static void AdjustIdForEfCore(this IEnumerable<IEntity<int>> items)
    {
        foreach (var item in items)
        {
            item.Id = Math.Max(0, item.Id);
        }
    }

    public static void SetSortOrder(this IEnumerable<ISortable> items)
    {
        var i = 0;
        foreach (var item in items)
        {
            item.SortOrder = i++;
        }
    }
}