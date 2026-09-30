using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Regira.Entities.Models;

/// <summary>
/// <see cref="EntityInputException.InputErrors"/>: a live, one-message-per-key view over
/// <see cref="EntityInputException.Errors"/>, so the two can never disagree. Reading a key joins its messages with a space;
/// setting a key replaces its messages with the one given, in the place of its first; removing a key removes them all.
/// Keys keep the order in which they first appear in <see cref="EntityInputException.Errors"/>.
/// </summary>
internal sealed class EntityInputErrorDictionary(EntityInputException owner) : IDictionary<string, string>
{
    private IList<EntityInputError> Errors => owner.Errors;

    public string this[string key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"No input error for '{key}'.");
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            var index = IndexOf(key);
            if (index < 0)
            {
                Errors.Add(new EntityInputError(key, value));
                return;
            }
            RemoveAll(key);
            Errors.Insert(index, new EntityInputError(key, value));
        }
    }

    public ICollection<string> Keys => Errors.Select(e => e.Key).Distinct(StringComparer.Ordinal).ToList();
    public ICollection<string> Values => Keys.Select(key => this[key]).ToList();
    public int Count => Errors.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count();
    public bool IsReadOnly => false;

    public void Add(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ContainsKey(key))
        {
            throw new ArgumentException($"An input error for '{key}' has already been added.", nameof(key));
        }
        Errors.Add(new EntityInputError(key, value));
    }
    public void Add(KeyValuePair<string, string> item) => Add(item.Key, item.Value);

    public bool ContainsKey(string key) => IndexOf(key) >= 0;
    public bool Contains(KeyValuePair<string, string> item) => TryGetValue(item.Key, out var value) && value == item.Value;

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var messages = Errors.Where(e => e.Key == key).Select(e => e.Message).ToList();
        value = messages.Count == 0 ? null : string.Join(" ", messages);
        return value != null;
    }

    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return RemoveAll(key) > 0;
    }
    public bool Remove(KeyValuePair<string, string> item) => Contains(item) && Remove(item.Key);

    public void Clear() => Errors.Clear();

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => this.ToList().CopyTo(array, arrayIndex);

    // over the keys as they stood when the loop began, passing over one removed since: a Dictionary lets a foreach remove entries
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            if (TryGetValue(key, out var value))
            {
                yield return new KeyValuePair<string, string>(key, value);
            }
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int IndexOf(string key)
    {
        for (var i = 0; i < Errors.Count; i++)
        {
            if (Errors[i].Key == key)
            {
                return i;
            }
        }
        return -1;
    }

    private int RemoveAll(string key)
    {
        var removed = 0;
        for (var i = Errors.Count - 1; i >= 0; i--)
        {
            if (Errors[i].Key == key)
            {
                Errors.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }
}
