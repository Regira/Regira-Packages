using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using static System.Convert;

namespace Regira.Utilities;

public static class CollectionUtility
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Cache = new();

    /// <summary>
    /// Converts the specified enumerable collection to a <see cref="List{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The enumerable collection to convert. Can be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="List{T}"/> containing the elements of the collection. 
    /// If <paramref name="items"/> is <c>null</c>, an empty list is returned. 
    /// If <paramref name="items"/> is already a <see cref="List{T}"/>, it is returned as-is.
    /// </returns>
    public static List<T> AsList<T>(this IEnumerable<T>? items)
    {
        return items switch
        {
            null => [],
            List<T> list => list,
            _ => items.ToList()
        };
    }
    /// <summary>
    /// Returns a sequence of distinct elements from the provided collection based on a specified key selector function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection of elements to filter for distinct values.</param>
    /// <param name="selector">
    /// A function to extract the key for each element. The key is used to determine distinctness.
    /// </param>
    /// <param name="returnLastValue">
    /// A boolean value indicating whether to return the last occurrence of each distinct element.
    /// If <c>true</c>, the last occurrence is returned; otherwise, the first occurrence is returned.
    /// </param>
    /// <returns>
    /// A sequence of distinct elements from the input collection, based on the specified key selector.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="items"/> or <paramref name="selector"/> is <c>null</c>.
    /// </exception>
    public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> items, Func<T, object> selector, bool returnLastValue = false)
    {
        //return items.GroupBy(selector).Select(x => returnLastValue ? x.LastOrDefault() : x.FirstOrDefault());
        return DistinctBy(items, selector, x => returnLastValue ? x.Last() : x.First());
    }
    /// <summary>
    /// Returns a sequence of distinct elements from the provided collection based on a specified key selector function
    /// and a custom distinct element selector.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection of elements to filter for distinct values.</param>
    /// <param name="groupSelector">
    /// A function to extract the key for each element. The key is used to group elements for distinctness.
    /// </param>
    /// <param name="distinctSelector">
    /// A function to select a single element from each group of elements sharing the same key.
    /// </param>
    /// <returns>
    /// A sequence of distinct elements from the input collection, determined by the specified key selector and distinct selector.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="items"/>, <paramref name="groupSelector"/>, or <paramref name="distinctSelector"/> is <c>null</c>.
    /// </exception>
    public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> items, Func<T, object> groupSelector, Func<IGrouping<object, T>, T> distinctSelector)
    {
        return items.GroupBy(groupSelector).Select(distinctSelector);
    }

    // https://stackoverflow.com/questions/481603/set-extend-listt-length-in-c-sharp#answer-481658
    /// <summary>
    /// Ensures that the specified list has at least the specified size.
    /// If the list is smaller than the specified size, it is expanded with default values.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to ensure the size of. Cannot be <c>null</c>.</param>
    /// <param name="size">The desired size of the list. Must be greater than or equal to 0.</param>
    /// <returns>
    /// The original list, expanded with default values if necessary, to meet the specified size.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="list"/> is <c>null</c>.</exception>
    public static List<T?> EnsureSize<T>(this List<T> list, int size)
    {
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }

        return EnsureSize(list!, size, default);
    }
    /// <summary>
    /// Ensures that the specified list has at least the specified size, filling it with the specified value if necessary.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to ensure the size of. Cannot be <c>null</c>.</param>
    /// <param name="size">The minimum size the list should have. Must be greater than or equal to 0.</param>
    /// <param name="value">The value to use for filling the list if its size is less than the specified size.</param>
    /// <returns>The original list, with its size adjusted if necessary.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="list"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is less than 0.</exception>
    public static List<T> EnsureSize<T>(this List<T> list, int size, T value)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var count = list.Count;
        if (count < size)
        {
            var capacity = list.Capacity;
            if (capacity < size)
            {
                list.Capacity = Math.Max(size, capacity * 2);
            }

            while (count < size)
            {
                list.Add(value);
                ++count;
            }
        }

        return list;
    }
    /// <summary>
    /// Adjusts the size of the specified list to the given size, filling it with the specified default value if necessary.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to adjust the size of. Cannot be <c>null</c>.</param>
    /// <param name="size">The desired size of the list. Must be greater than or equal to 0.</param>
    /// <param name="defaultValue">
    /// The default value to use for filling the list if its size is less than the specified size. 
    /// If not provided, the default value for type <typeparamref name="T"/> is used.
    /// </param>
    /// <returns>The original list, with its size adjusted to the specified size.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="list"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is less than 0.</exception>
    public static List<T?> SetSize<T>(this List<T> list, int size, T? defaultValue = default)
    {
        return EnsureSize(list!, size, defaultValue);
    }
    /// <summary>
    /// Repeats the elements of the specified sequence a given number of times.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="items">The sequence of elements to repeat.</param>
    /// <param name="times">The number of times to repeat the sequence.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the repeated elements of the input sequence.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="times"/> is less than zero.</exception>
    public static IEnumerable<T> Repeat<T>(this IEnumerable<T> items, int times)
    {
        var list = items.AsList();
        for (var i = 0; i < times; i++)
        {
            foreach (var item in list)
            {
                yield return item;
            }
        }
    }
    
    /// <summary>
    /// Sorts the elements of the specified collection based on a property and sort direction.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection of items to sort.</param>
    /// <param name="sortBy">
    /// A string specifying the property to sort by and the sort direction. 
    /// The format is "PropertyName [asc|desc]". If no direction is specified, ascending order is used by default.
    /// </param>
    /// <returns>
    /// A sorted <see cref="IEnumerable{T}"/> based on the specified property and direction. 
    /// If the property is not found, the original collection is returned.
    /// </returns>
    public static IEnumerable<T> SortItems<T>(this IEnumerable<T> items, string sortBy)
    {
        var itemProperties = GetItemProperties<T>();
        var sortSegments = sortBy.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var sortProperty = itemProperties.FirstOrDefault(p => p.Name.Equals(sortSegments.First(), StringComparison.CurrentCultureIgnoreCase));
        if (sortProperty != null)
        {
            var sortDirection = sortSegments.Length > 1 ? sortSegments[1] : "asc";
            items = "desc".Equals(sortDirection, StringComparison.CurrentCultureIgnoreCase)
                ? items.OrderByDescending(x => sortProperty.GetValue(x))
                : items.OrderBy(x => sortProperty.GetValue(x));
        }

        return items;
    }
    /// <summary>
    /// Retrieves a subset of items from the specified collection, based on the provided page size and page index.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection of items to paginate.</param>
    /// <param name="pageSize">
    /// The number of items to include in each page. Must be greater than zero.
    /// </param>
    /// <param name="pageIndex">
    /// The zero-based index of the page to retrieve. If less than zero, it will be treated as zero.
    /// </param>
    /// <returns>
    /// A sequence containing the items in the specified page. If the collection contains fewer items than the requested page, an empty sequence is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="pageSize"/> is less than or equal to zero.
    /// </exception>
    public static IEnumerable<T> PageItems<T>(this IEnumerable<T> items, int pageSize, int pageIndex)
    {
        if (pageIndex > 0)
        {
            items = items.Skip(pageSize * pageIndex);
        }

        items = items.Take(pageSize);

        return items;
    }

    /// <summary>
    /// Processes the elements of the specified collection in chunks, applying the provided asynchronous action to each element.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the asynchronous action.</typeparam>
    /// <param name="items">The collection of elements to process.</param>
    /// <param name="action">
    /// An asynchronous function to apply to each element in the collection.
    /// The function takes an element of type <typeparamref name="T"/> and returns a <see cref="Task{TResult}"/>.
    /// </param>
    /// <param name="size">
    /// The size of each chunk. Defaults to 10 if not specified. 
    /// Must be greater than zero.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. 
    /// The task result is a list of results of type <typeparamref name="TResult"/> produced by the action.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="items"/> or <paramref name="action"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="size"/> is less than or equal to zero.
    /// </exception>
    public static async Task<IList<TResult>> ChunkActionsAsync<T, TResult>(this IEnumerable<T> items, Func<T, Task<TResult>> action, int size = 10)
    {
        var results = new List<TResult>();

        var chunks = items.Chunk(size);
        foreach (var chunk in chunks)
        {
            var tasks = chunk.Select(action);
            var chunkResults = await Task.WhenAll(tasks);
            results.AddRange(chunkResults);
        }

        return results;
    }

    /// <summary>
    /// Randomly shuffles the elements of the specified <see cref="IEnumerable{T}"/> sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="source">The sequence to shuffle.</param>
    /// <returns>A new sequence with the elements of the original sequence in random order.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="source"/> is <c>null</c>.</exception>
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        // https://stackoverflow.com/questions/5807128/an-extension-method-on-ienumerable-needed-for-shuffling#answer-5807238
        if (source == null) throw new ArgumentNullException(nameof(source));

        return source.ShuffleIterator(new Random());
    }
    private static IEnumerable<T> ShuffleIterator<T>(this IEnumerable<T> source, Random rng)
    {
        var buffer = source.ToList();
        for (var i = 0; i < buffer.Count; i++)
        {
            var j = rng.Next(i, buffer.Count);
            yield return buffer[j];

            buffer[j] = buffer[i];
        }
    }

    /// <summary>
    /// Filters a collection of items based on the properties and values provided in the <paramref name="searchObject"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of elements in the collection.
    /// </typeparam>
    /// <param name="items">
    /// The collection of items to filter. Cannot be <c>null</c>.
    /// </param>
    /// <param name="searchObject">
    /// An object containing the properties and values to filter by. The properties of this object
    /// will be matched against the properties of the items in the collection.
    /// </param>
    /// <returns>
    /// A filtered collection of items that match all the specified properties and values in the <paramref name="searchObject"/>.
    /// </returns>
    /// <remarks>
    /// This method uses reflection to compare the properties of the items in the collection with the properties
    /// of the <paramref name="searchObject"/>. It supports filtering by various data types, including enums, integers,
    /// and strings. The filtering is case-insensitive for string comparisons.
    /// </remarks>
    /// <example>
    /// The following example demonstrates how to filter a collection of persons by their weight:
    /// <code>
    /// var filteredPersons = persons.FilterItems(new { weight = 55 });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="items"/> is <c>null</c>.
    /// </exception>
    public static IEnumerable<T> FilterItems<T>(this IEnumerable<T> items, object searchObject)
        => FilterItems(items, DictionaryUtility.ToDictionary(searchObject));
    /// <summary>
    /// Filters a collection of items based on the specified search criteria.
    /// </summary>
    /// <typeparam name="T">The type of the items in the collection.</typeparam>
    /// <param name="items">The collection of items to filter.</param>
    /// <param name="searchObject">
    /// A dictionary containing key-value pairs that represent the search criteria.
    /// The keys correspond to the properties of the items, and the values represent the expected values for filtering.
    /// </param>
    /// <returns>
    /// A filtered collection of items that match the specified search criteria.
    /// </returns>
    public static IEnumerable<T> FilterItems<T>(this IEnumerable<T> items, IDictionary<string, object?> searchObject)
    {
        var flatSo = searchObject.Flatten();

        return items
            .Where(x => flatSo.All(kvp =>
            {
                var itemDic = DictionaryUtility.ToDictionary(x, new DictionaryOptions { IncludeNulls = true }).Flatten(new FlattenOptions { IgnoreCollections = true }
                );

                var searchKey = kvp.Key;
                var searchValue = kvp.Value;
                var itemKey = FindItemKey(itemDic.Keys, searchKey);

                if (itemKey == null)
                {
                    return false;
                }

                var itemValue = itemDic[itemKey];
                if (itemValue != null)
                {
                    var simpleType = TypeUtility.GetSimpleType(itemValue.GetType());
                    if (simpleType.IsEnum)
                    {
                        return CompareEnums(itemValue, searchValue, simpleType);
                    }

                    var simpleSearchValue = ChangeType(searchValue, simpleType);

                    var minMaxResult = CompareMinMax(itemValue, simpleSearchValue, searchKey);
                    if (minMaxResult.HasValue)
                    {
                        return minMaxResult.Value;
                    }

                    if (simpleType == typeof(string))
                    {
                        return CompareStrings(itemValue, simpleSearchValue);
                    }
                }

                return itemValue == searchValue || searchValue != null && searchValue.Equals(itemValue);
            }))
            .ToList();
    }
    private static string? FindItemKey(IEnumerable<string> itemKeys, string searchKey)
    {
        var searchKeySegments = searchKey.Split('.');
        var searchKeyPrefix = string.Join(".", searchKeySegments.Take(searchKeySegments.Length - 1));
        return itemKeys.FirstOrDefault(itemKey =>
        {
            if (itemKey.Equals(searchKey, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            var itemKeySegments = itemKey.Split('.');
            var itemKeyPrefix = string.Join(".", itemKeySegments.Take(itemKeySegments.Length - 1));
            return itemKeyPrefix.Equals(searchKeyPrefix, StringComparison.InvariantCultureIgnoreCase)
                   && (
                       $"Min{itemKeySegments.Last()}".Equals(searchKeySegments.Last(), StringComparison.InvariantCultureIgnoreCase)
                       || $"Max{itemKeySegments.Last()}".Equals(searchKeySegments.Last(), StringComparison.InvariantCultureIgnoreCase)
                   );
        });
    }
    private static bool CompareEnums(object itemValue, object? searchValue, Type enumType)
    {
        var enumString = searchValue?.ToString();
        // compare enum by int-value
        if (int.TryParse(enumString, out var enumInt))
        {
            return enumInt == (int)itemValue;
        }
        // compare enum by string-value
        var correctedEnumValue = Enum.GetNames(enumType).FirstOrDefault(n => n.Equals(enumString, StringComparison.InvariantCultureIgnoreCase));
        return correctedEnumValue != null && correctedEnumValue.Equals(itemValue.ToString());
    }
    private static bool? CompareMinMax(object itemValue, object? searchValue, string searchKey)
    {
        var searchKeySegments = searchKey.Split('.');
        var lastSegment = searchKeySegments.Last();
        if (lastSegment.StartsWith("Min", StringComparison.InvariantCultureIgnoreCase))
        {
            return ((IComparable)itemValue).CompareTo(searchValue) >= 0;
        }

        if (lastSegment.StartsWith("Max", StringComparison.InvariantCultureIgnoreCase))
        {
            return ((IComparable)itemValue).CompareTo(searchValue) <= 0;
        }

        return null;
    }
    private static bool CompareStrings(object itemValue, object? searchValue)
    {
        var searchString = searchValue?.ToString();
        var itemValueString = itemValue.ToString();

        if (searchString?.Contains('*') ?? false)
        {
            if (itemValueString != null)
            {
                var startsWith = searchString.StartsWith('*');
                var endsWith = searchString.EndsWith('*');
                var trimmedSearchString = searchString.Trim('*');
                if (startsWith && endsWith)
                {
                    return itemValueString.Contains(trimmedSearchString, StringComparison.InvariantCultureIgnoreCase);
                }
                if (startsWith)
                {
                    return itemValueString.EndsWith(trimmedSearchString, StringComparison.InvariantCultureIgnoreCase);
                }
                if (endsWith)
                {
                    return itemValueString.StartsWith(trimmedSearchString, StringComparison.InvariantCultureIgnoreCase);
                }
            }
        }

        return itemValueString?.Equals(searchString, StringComparison.InvariantCultureIgnoreCase) ?? false;
    }
    private static PropertyInfo[] GetItemProperties<T>()
    {
        var itemType = typeof(T);
        if (!Cache.TryGetValue(itemType, out var itemProperties))
        {
            itemProperties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.GetIndexParameters().Length == 0 && prop.GetMethod != null)
                .ToArray();

            Cache.TryAdd(itemType, itemProperties);
        }

        return itemProperties;
    }

    /// <summary>
    /// Creates a generic list of the specified type, optionally populating it with the provided values.
    /// </summary>
    /// <param name="genericType">
    /// The type of the elements in the generic list.
    /// </param>
    /// <param name="values">
    /// An optional collection of values to populate the list. If <c>null</c>, the list will be empty.
    /// </param>
    /// <param name="listType">
    /// An optional type of the list to create. If <c>null</c>, a default <see cref="System.Collections.Generic.List{T}"/> is used.
    /// </param>
    /// <returns>
    /// A generic list of the specified type, optionally populated with the provided values.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="genericType"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the specified <paramref name="listType"/> does not implement <see cref="System.Collections.IList"/>.
    /// </exception>
    public static IList CreateGenericList(Type genericType, ICollection? values = null, Type? listType = null)
    {
        var genericListType = TypeUtility.CreateGenericListType(genericType, listType);
        var list = (Activator.CreateInstance(genericListType) as IList)!;
        if (values?.Count > 0)
        {
            foreach (var value in values)
            {
                list.Add(value);
            }
        }
        return list;
    }
}