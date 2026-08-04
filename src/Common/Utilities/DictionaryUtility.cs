using System.Collections;
using System.Collections.Specialized;
using System.Reflection;

namespace Regira.Utilities;

#region Options
public class DictionaryOptions
{
    /// <summary>
    /// Include null values (default = true)
    /// </summary>
    public bool IncludeNulls { get; set; } = true;
    /// <summary>
    /// Make keys key insensitive (default = true)
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
    /// <summary>
    /// Process nested objects (default = true)
    /// </summary>
    public bool Recursive { get; set; } = true;
    /// <summary>
    /// Renames a duplicate key, otherwise overwrites existing value (default = false)
    /// </summary>
    public bool RenameDuplicateKeys { get; set; }
}
public class FlattenOptions
{
    /// <summary>
    /// Character(s) between properties (default = ".")
    /// </summary>
    public string Separator { get; set; } = ".";
    /// <summary>
    /// Exclude flattening collection values (default false)
    /// </summary>
    public bool IgnoreCollections { get; set; }
}
public class TableArrayOptions
{
    /// <summary>
    /// Expect headers in the first row
    /// </summary>
    public bool HeadersOnFirstRow { get; set; } = true;
    /// <summary>
    /// Exclude null values (default = false)
    /// </summary>
    public bool IgnoreNulls { get; set; } = false;
}
#endregion

public static class DictionaryUtility
{
    /// <summary>
    /// Converts a <see cref="NameValueCollection"/> to a dictionary where the keys are strings and the values are nullable strings.
    /// </summary>
    /// <param name="collection">The <see cref="NameValueCollection"/> to convert.</param>
    /// <returns>
    /// A dictionary containing the keys and values from the <paramref name="collection"/>.
    /// If the collection contains duplicate keys, only the first occurrence is included in the resulting dictionary.
    /// </returns>
    public static IDictionary<string, string?> ToDictionary(this NameValueCollection collection)
    {
        IDictionary<string, string?> dic = new Dictionary<string, string?>();
        var distinctKeys = collection.AllKeys.Distinct().ToArray();
        foreach (var key in distinctKeys)
        {
            dic.Add(key!, collection[key]);
        }
        return dic;
    }

    /// <summary>
    /// Keys should start with '-'<br />
    /// A value arg should be preceded with a key arg<br />
    /// A value can be null
    /// </summary>
    /// <param name="args"></param>
    /// <param name="keyPrefix"></param>
    /// <returns></returns>
    public static IDictionary<string, string?> ToDictionary(this IEnumerable<string> args, string keyPrefix = "-")
    {
        var dic = new Dictionary<string, string?>(StringComparer.InvariantCultureIgnoreCase);

        void Add(Queue<string> queue)
        {
            if (queue.Count > 0)
            {
                var arg = queue.Dequeue();
                if (arg.StartsWith(keyPrefix))
                {
                    var key = arg.Substring(1);
                    if (queue.Count > 0)
                    {
                        var value = queue.Peek();
                        dic.Add(key, value.StartsWith(keyPrefix) != true ? queue.Dequeue() : null);
                    }
                    else
                    {
                        dic.Add(key, null);
                    }
                }
                Add(queue);
            }
        }
        Add(new Queue<string>(args));

        return dic;
    }

    /// <summary>
    /// Converts an <see cref="IEnumerable{T}"/> to a dictionary using the specified key and element selectors.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements in the source collection.</typeparam>
    /// <typeparam name="TElement">The type of the elements in the resulting dictionary.</typeparam>
    /// <param name="source">The source collection to convert to a dictionary.</param>
    /// <param name="keySelector">A function to extract the key for each element.</param>
    /// <param name="elementSelector">A function to extract the value for each element.</param>
    /// <param name="renameDuplicateKeys">
    /// A boolean value indicating whether duplicate keys should be renamed by appending an index to make them unique.
    /// </param>
    /// <returns>
    /// A dictionary containing keys and values extracted from the source collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="source"/>, <paramref name="keySelector"/>, or <paramref name="elementSelector"/> is <c>null</c>.
    /// </exception>
    public static IDictionary<string, TElement> ToDictionary<TSource, TElement>(this IEnumerable<TSource> source, Func<TSource, string> keySelector, Func<TSource, TElement> elementSelector, bool renameDuplicateKeys)
    {
        var pairs = source.Select(x => new KeyValuePair<string, TElement>(keySelector(x), elementSelector(x)));
        var dic = new Dictionary<string, TElement>();
        foreach (var kvp in pairs)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            var index = 2;
            while (renameDuplicateKeys && dic.ContainsKey(key))
            {
                key = $"{kvp.Key}{index}";
            }
            dic[key] = value;
        }
        return dic;
    }
    /// <summary>
    /// Converts the specified object into a dictionary representation.
    /// </summary>
    /// <param name="source">
    /// The object to be converted into a dictionary. Can be <c>null</c>.
    /// </param>
    /// <param name="options">
    /// Options for customizing the dictionary creation process, such as case sensitivity.
    /// If <c>null</c>, default options will be used.
    /// </param>
    /// <returns>
    /// A dictionary containing the properties of the object as key-value pairs.
    /// If <paramref name="options"/> specifies case-insensitivity, the keys will be case-insensitive.
    /// </returns>
    /// <remarks>
    /// This method is useful for scenarios where an object's properties need to be represented
    /// as a dictionary for further processing, such as serialization or filtering.
    /// </remarks>
    public static IDictionary<string, object?> ToDictionary(object? source, DictionaryOptions? options = null)
    {
        options ??= new DictionaryOptions();

        var dic = CreateDictionary(source, options);
        if (options.IgnoreCase)
        {
            return new Dictionary<string, object?>(dic, StringComparer.InvariantCultureIgnoreCase);
        }

        return dic;
    }
    private static IDictionary<string, object?> CreateDictionary(object? source, DictionaryOptions options)
    {
        if (source == null || source is string)
        {
            return new Dictionary<string, object?>();
        }
        if (source is IDictionary<string, object?> objects)
        {
            return objects;
        }
        if (source is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return pairs.ToDictionary(k => k.Key, v => v.Value);
        }
        if (source is IEnumerable<KeyValuePair<string, string?>> stringPairs)
        {
            return stringPairs.ToDictionary(x => x.Key, x => (object?)x.Value);
        }
        if (source is NameValueCollection nameValues)
        {
            var nvDic = new Dictionary<string, object?>();
            foreach (var key in nameValues.AllKeys)
            {
                var value = nameValues[key];
                if (value != null || options.IncludeNulls)
                {
                    nvDic.Add(key!, value);
                }
            }
            return nvDic;
        }


        var dic = new Dictionary<string, object?>();
        foreach (var property in PropertyUtility.GetProperties(source))
        {
            var value = property.GetValue(source);
            if (value == null && !options.IncludeNulls)
            {
                continue;
            }

            var key = property.Name;
            if (options.RenameDuplicateKeys)
            {
                var index = 2;
                while (dic.ContainsKey(key))
                {
                    key = $"{property.Name}_{index}";
                }
            }

            dic[key] = GetValue(value, options.Recursive, options.IncludeNulls, options.RenameDuplicateKeys);
        }
        return dic;
    }
    
    /// <summary>
    /// Retrieves the processed value of an object, with optional recursion and customization for null handling and duplicate key renaming.
    /// </summary>
    /// <param name="value">The object whose value is to be processed.</param>
    /// <param name="recursive">
    /// A boolean indicating whether to recursively process nested objects or collections.
    /// </param>
    /// <param name="includeNulls">
    /// A boolean indicating whether to include null values in the processed result.
    /// </param>
    /// <param name="renameDuplicateKeys">
    /// A boolean indicating whether to rename duplicate keys when processing objects into dictionaries.
    /// </param>
    /// <returns>
    /// The processed value, which could be a primitive type, a collection, or a dictionary, depending on the input and options provided.
    /// </returns>
    public static object? GetValue(object? value, bool recursive, bool includeNulls, bool renameDuplicateKeys)
    {
        var valueType = value?.GetType();
        if (value == null || valueType == null || !recursive || TypeUtility.IsSimpleType(valueType) || TypeUtility.IsTypeACollection(valueType))
        {
            if (valueType != null && !TypeUtility.IsSimpleType(valueType) && value is IList list)//!TypeUtility.IsSimpleType(valueType) && TypeUtility.IsTypeACollection(valueType))
            {
                var listValues = new List<object?>(list.Count);
                foreach (var item in list)
                {
                    var listValue = GetValue(item, recursive, includeNulls, renameDuplicateKeys);
                    listValues.Add(listValue);
                }
                return listValues;
            }
        }
        else
        {
            return CreateDictionary(value, new DictionaryOptions { IncludeNulls = includeNulls, Recursive = true, RenameDuplicateKeys = renameDuplicateKeys });
        }

        return value;
    }
    /// <summary>
    /// Filters out entries with null values from the provided dictionary and returns a new dictionary with non-null values.
    /// </summary>
    /// <param name="source">The source dictionary to filter.</param>
    /// <returns>A new dictionary containing only the entries with non-null values from the source dictionary.</returns>
    public static IDictionary<string, object> NonNullable(this IDictionary<string, object?> source)
        => source.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!);

    /// <summary>
    /// Flattens a nested dictionary into a single-level dictionary with concatenated keys.
    /// </summary>
    /// <param name="source">The source dictionary to flatten.</param>
    /// <param name="options">
    /// Optional <see cref="FlattenOptions"/> to configure the flattening behavior, such as key separator or collection handling.
    /// </param>
    /// <returns>
    /// A flattened dictionary where nested keys are concatenated using the specified separator.
    /// </returns>
    /// <remarks>
    /// This method supports flattening nested dictionaries, collections of dictionaries, and simple collections.
    /// It can also handle scenarios where collections are ignored based on the provided options.
    /// </remarks>
    public static IDictionary<string, object?> Flatten(this IDictionary<string, object?> source, FlattenOptions? options = null)
    {
        options ??= new FlattenOptions();

        var target = new Dictionary<string, object?>();

        string BuildPrefix(string prefix, string key) => $"{prefix}{options.Separator}{key}".Trim(options.Separator.ToCharArray());
        void ParseRoot(IDictionary<string, object?> root, string prefix = "")
        {
            foreach (var kv in root)
            {
                SetItem(kv.Key, kv.Value, prefix);
            }
        }
        void SetItem(string key, object? value, string prefix = "")
        {
            var fullKey = BuildPrefix(prefix, key);
            // nested dictionary ({a:1, b:{c:2}})
            if (value is IDictionary<string, object?> dic)
            {
                var newPrefix = fullKey;
                ParseRoot(dic, newPrefix);
            }
            // collection of dictionaries ([{a:1},{b:2}])
            else if (value is IEnumerable<IDictionary<string, object?>> collection)
            {
                var list = collection.ToList();
                if (options.IgnoreCollections)
                {
                    target[fullKey] = list.Select(item => Flatten(item, options)).ToList();
                }
                else
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        ParseRoot(list[i], BuildPrefix(fullKey, i.ToString()));
                    }
                }
            }
            // simple collection ([1,2,3] or ["a","b","c"])
            else if (value != null && !TypeUtility.IsSimpleType(value.GetType()) && value is IEnumerable simpleCollection && !options.IgnoreCollections)
            {
                var arr = simpleCollection.Cast<object?>().ToArray();
                for (var i = 0; i < arr.Length; i++)
                {
                    var indexedKey = BuildPrefix(fullKey, i.ToString());
                    target[indexedKey] = arr[i];
                }
            }
            // simple value (1, "string", true, null, DateTime, etc)
            else
            {
                target[fullKey] = value;
            }
        }

        ParseRoot(source);
        return target;
    }
    /// <summary>
    /// Converts a flattened dictionary structure into a hierarchical dictionary structure.
    /// </summary>
    /// <param name="source">
    /// The source dictionary containing flattened key-value pairs.
    /// Keys are expected to represent hierarchical paths, separated by a delimiter.
    /// </param>
    /// <param name="options">
    /// Optional configuration for unflattening, such as the delimiter used to separate key segments.
    /// If not provided, default options will be used.
    /// </param>
    /// <returns>
    /// A hierarchical dictionary representation of the input flattened dictionary.
    /// </returns>
    /// <remarks>
    /// This method processes keys in the source dictionary to reconstruct nested structures,
    /// supporting both dictionaries and lists as intermediate or final values.
    /// </remarks>
    public static IDictionary<string, object?> Unflatten(this IDictionary<string, object?> source, FlattenOptions? options = null)
    {
        options ??= new FlattenOptions();

        string[] KeySegments(string key)
        {
            return key.Split(options.Separator.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }
        string FirstKeySegment(string key)
        {
            return KeySegments(key).First();
            //return key.Substring(0, key.IndexOf(separator, StringComparison.Ordinal));
        }
        string NextKeyString(string key)
        {
            return key.Substring(key.IndexOf(options.Separator, StringComparison.Ordinal) + 1);
        }
        IDictionary<string, object?> ParseRoot(IDictionary<string, object?> root)
        {
            var target = new Dictionary<string, object?>();
            foreach (var kv in root)
            {
                SetItem(kv.Key, kv.Value, target);
            }
            return target;
        }
        void AddHierarchy(string key, object? value, IDictionary<string, object?> target)
        {
            var parentKey = FirstKeySegment(key);
            var newKeyString = NextKeyString(key);
            var nextKey = FirstKeySegment(newKeyString);

            var isNextKeyAnIndex = int.TryParse(nextKey, out _);
            // last segment is an index -> simple array
            var isSimpleArray = key.Split(options.Separator.ToCharArray()).Last() == nextKey;

            if (!target.ContainsKey(parentKey))
            {
                object parent = isNextKeyAnIndex switch
                {
                    true when isSimpleArray => new List<object>(),
                    true => new List<Dictionary<string, object?>>(),
                    _ => new Dictionary<string, object?>()
                };

                target[parentKey] = parent;
            }

            if (isNextKeyAnIndex)
            {
                if (isSimpleArray)
                {
                    SetSimpleListItem(newKeyString, value, (List<object?>)target[parentKey]!);
                }
                else
                {
                    SetListItem(newKeyString, value, (List<Dictionary<string, object?>?>)target[parentKey]!);
                }
            }
            else
            {
                SetItem(newKeyString, value, target[parentKey]);
            }
        }

        void SetSimpleListItem(string key, object? value, List<object?> target)
        {
            var index = int.Parse(FirstKeySegment(key));
            target.EnsureSize(index + 1);
            target[index] = value;
        }
        void SetListItem(string key, object? value, List<Dictionary<string, object?>?> target)
        {
            var index = int.Parse(FirstKeySegment(key));
            var newKeyString = NextKeyString(key);
            if (target.Count <= index)
            {
                target.Add(new Dictionary<string, object?>());
            }
            SetItem(newKeyString, value, target[index]);
        }
        void SetItem(string key, object? value, object? target)
        {
            if (target is IDictionary<string, object?> dic)
            {
                if (key.Contains(options.Separator))
                {
                    AddHierarchy(key, value, dic);
                }
                else if (value is IEnumerable<IDictionary<string, object?>> collection)
                {
                    dic[key] = collection
                        .Select(ParseRoot)
                        .ToList();
                }
                else
                {
                    dic[key] = value;
                }
            }
            else if (target is List<object?> list)
            {
                var index = int.Parse(key);
                list.EnsureSize(index);
                list[index] = value;
            }
        }

        return ParseRoot(source);
    }

    /// <summary>
    /// Converts a collection of dictionaries into a two-dimensional array (table format).
    /// </summary>
    /// <param name="listOfDictionaries">
    /// The collection of dictionaries to be converted. Each dictionary represents a row in the resulting table.
    /// </param>
    /// <param name="options">
    /// Optional configuration for the table generation, such as whether to include headers in the first row
    /// and whether to ignore null values.
    /// </param>
    /// <returns>
    /// A two-dimensional array where each row corresponds to a dictionary and each column corresponds to a unique key
    /// from the dictionaries. If <paramref name="options"/> specifies headers, the first row contains the keys.
    /// </returns>
    public static object?[,] ToTableArray(this IEnumerable<IDictionary<string, object?>> listOfDictionaries, TableArrayOptions? options = null)
    {
        options ??= new TableArrayOptions();

        var dicList = listOfDictionaries.ToArray();
        var keys = dicList
            .SelectMany(dic => dic.Keys).Distinct().ToArray();
        var table = new object?[dicList.Length + 1, keys.Length];

        var startRow = 0;
        if (options.HeadersOnFirstRow)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                table[0, i] = keys[i];
            }

            startRow = 1;
        }

        for (var i = 0; i < keys.Length; i++)
        {
            for (var row = startRow; row <= dicList.Length; row++)
            {
                var header = keys[i];
                var dic = dicList[row - startRow];
                if (dic.TryGetValue(header, out var value))
                {
                    table[row, i] = value;
                }
            }
        }

        return table;
    }
    /// <summary>
    /// Converts a two-dimensional array into a collection of dictionaries, where each dictionary represents a row in the array.
    /// </summary>
    /// <param name="table">
    /// A two-dimensional array of objects to be converted. The array must have exactly two dimensions.
    /// </param>
    /// <param name="options">
    /// Options for customizing the conversion process, such as whether the first row contains headers and whether to ignore null values.
    /// </param>
    /// <returns>
    /// An enumerable collection of dictionaries, where each dictionary represents a row in the array.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the provided array does not have exactly two dimensions.
    /// </exception>
    public static IEnumerable<IDictionary<string, object?>> FromTableArray(object?[,] table, TableArrayOptions? options = null)
    {
        options ??= new TableArrayOptions();

        if (table.Rank != 2)
        {
            throw new NotSupportedException("Array must have 2 dimensions -> [,]");
        }

        var rowCount = table.GetLength(0) - (options.HeadersOnFirstRow ? 1 : 0);
        var colCount = table.GetLength(1);
        var dicList = new List<Dictionary<string, object?>>(rowCount);

        var keys = new string[colCount];
        for (var row = 0; row < table.GetLength(0); row++)
        {
            var dic = new Dictionary<string, object?>(colCount);
            for (var col = 0; col < table.GetLength(1); col++)
            {
                if (row == 0)
                {
                    if (options.HeadersOnFirstRow)
                    {
                        keys[col] = table[row, col]?.ToString()!;
                    }
                    else
                    {
                        keys[col] = $"Field-{col}";
                    }
                }

                if (row > 0 || !options.HeadersOnFirstRow)
                {
                    var value = table[row, col];
                    if (value != null || !options.IgnoreNulls)
                    {
                        var key = keys[col];
                        dic[key] = value;
                    }
                }
            }

            if (row > 0 || !options.HeadersOnFirstRow)
            {
                dicList.Add(dic);
            }
        }

        return dicList;
    }

    /// <summary>
    /// Ensures that the values in the dictionary are type-safe by converting them to match the property types of the specified class.
    /// </summary>
    /// <typeparam name="T">The target class type whose properties will be used for type conversion.</typeparam>
    /// <param name="dic">The dictionary whose values will be type-checked and converted.</param>
    /// <param name="properties">
    /// An optional array of <see cref="PropertyInfo"/> objects representing the properties of the target class.
    /// If not provided, the properties of the type <typeparamref name="T"/> will be used.
    /// </param>
    public static void MakeTypeSafe<T>(this IDictionary<string, object?> dic, PropertyInfo[]? properties = null)
        where T : class, new()
    {
        properties ??= typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            if (dic[prop.Name] != null && dic.ContainsKey(prop.Name) && dic[prop.Name]?.GetType() != TypeUtility.GetSimpleType(prop.PropertyType))
            {
                dic[prop.Name] = Convert.ChangeType(dic[prop.Name], TypeUtility.GetSimpleType(prop.PropertyType));
            }
        }
    }
    /// <summary>
    /// Converts a collection of dictionaries into type-safe objects of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type to which the dictionaries will be converted. 
    /// Must be a class with a parameterless constructor.
    /// </typeparam>
    /// <param name="collection">
    /// The collection of dictionaries to be converted. Each dictionary represents the properties of an object of type <typeparamref name="T"/>.
    /// </param>
    /// <remarks>
    /// This method ensures that the dictionaries in the collection are transformed into objects of the specified type <typeparamref name="T"/>.
    /// It uses reflection to map dictionary keys to the properties of the target type.
    /// </remarks>
    public static void MakeTypeSafe<T>(this IEnumerable<IDictionary<string, object?>> collection)
        where T : class, new()
    {
        var properties = typeof(T).GetProperties();

        foreach (var dic in collection)
        {
            MakeTypeSafe<T>(dic, properties);
        }
    }

    /// <summary>
    /// Populates the properties of the specified target object of type <typeparamref name="T"/> 
    /// using the key-value pairs from the provided dictionary.
    /// </summary>
    /// <typeparam name="T">The type of the target object to populate.</typeparam>
    /// <param name="input">The dictionary containing property names as keys and their corresponding values.</param>
    /// <param name="target">The target object whose properties will be populated.</param>
    /// <returns>The populated target object of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// The method performs a case-insensitive match between dictionary keys and the property names of the target object.
    /// Only writable properties without index parameters are considered for population.
    /// </remarks>
    public static T ToObject<T>(this IDictionary<string, object?> input, T target)
    {
        var caseInsensitiveInput = new Dictionary<string, object?>(input, StringComparer.InvariantCultureIgnoreCase);
        var targetProperties = typeof(T).GetProperties()
            .Where(prop => prop.GetIndexParameters().Length == 0 && prop.SetMethod != null);
        foreach (var property in targetProperties)
        {
            if (caseInsensitiveInput.TryGetValue(property.Name, out var value))
            {
                property.SetValue(target!, value, null);
            }
        }

        return target;
    }
    /// <summary>
    /// Converts the specified dictionary into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to create and populate.</typeparam>
    /// <param name="input">The dictionary containing property names as keys and their corresponding values.</param>
    /// <returns>A new instance of type <typeparamref name="T"/> with its properties populated from the dictionary.</returns>
    /// <remarks>
    /// The method performs a case-insensitive match between dictionary keys and the property names of the target object.
    /// Only writable properties without index parameters are considered for population.
    /// </remarks>
    public static T ToObject<T>(this IDictionary<string, object?> input)
        where T : new()
        => ToObject(input, new T());

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key from the dictionary
    /// and converts it to the specified type if possible.
    /// </summary>
    /// <typeparam name="T">The type of the values in the dictionary.</typeparam>
    /// <typeparam name="TResult">The type to which the value should be converted.</typeparam>
    /// <param name="dic">The dictionary to search for the key.</param>
    /// <param name="key">The key whose value to retrieve.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified key, 
    /// converted to the specified type, if the key is found and the conversion is successful; 
    /// otherwise, the default value for the type of the <typeparamref name="TResult"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the dictionary contains an element with the specified key 
    /// and the value can be successfully converted to the specified type; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetValue<T, TResult>(this IDictionary<string, T> dic, string key, out TResult? value)
    {
        if (dic.TryGetValue(key, out var result))
        {
            // try direct cast first
            if (result is TResult typedResult)
            {
                value = typedResult;
                return true;
            }

            try
            {
                // try convert
                value = (TResult?)Convert.ChangeType(result, typeof(TResult));
                return true;
            }
            catch
            {
                // continue below and return false
            }
        }

        // not found or cannot convert
        value = default;
        return false;
    }
    /// <summary>
    /// Attempts to retrieve the first value associated with the specified key from the given <see cref="ILookup{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the lookup.</typeparam>
    /// <typeparam name="TValue">The type of the values in the lookup.</typeparam>
    /// <param name="values">The <see cref="ILookup{TKey, TValue}"/> to search.</param>
    /// <param name="key">The key to locate in the lookup.</param>
    /// <param name="value">
    /// When this method returns, contains the first value associated with the specified key, 
    /// if the key is found; otherwise, the default value for the type of the value parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the lookup contains an element with the specified key; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetValue<TKey, TValue>(this ILookup<TKey, TValue> values, TKey key, out TValue? value)
    {
        var items = values.FirstOrDefault(x => x.Key!.Equals(key));
        if (items?.Any() != true)
        {
            value = default;
            return false;
        }

        value = items.FirstOrDefault();

        return true;
    }
}