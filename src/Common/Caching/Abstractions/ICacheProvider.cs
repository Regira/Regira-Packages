using System.Collections;

namespace Regira.Caching.Abstractions;

/// <summary>
/// Defines the contract for a caching provider, enabling storage, retrieval, and management of cached data.
/// </summary>
/// <remarks>
/// Implementations of this interface provide mechanisms to interact with a cache, including operations
/// such as adding, retrieving, removing, and clearing cached items. Additionally, it supports enumerating
/// through cached keys and accessing items via an indexer.
/// </remarks>
public interface ICacheProvider : IEnumerable
{
    IList<string> Keys { get; }
    object? this[string key] { get; set; }

    T? Get<T>(string key);
    /// <param name="key">The entry's key</param>
    /// <param name="value">The value to cache</param>
    /// <param name="duration">How long the entry lives, in seconds; <c>null</c> takes the provider's default. A provider without expiry ignores it</param>
    void Set<T>(string key, T? value, int? duration = null);
    void Remove(string key);
    void RemoveAll();

    new IDictionaryEnumerator GetEnumerator();
}