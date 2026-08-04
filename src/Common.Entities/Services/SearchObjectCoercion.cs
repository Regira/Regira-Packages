using Regira.Utilities;

namespace Regira.Entities.Services;

public static class SearchObjectCoercion
{
    /// <summary>
    /// Coerces an untyped search object (e.g. an anonymous object like <c>new { Id = 5 }</c>) into
    /// <typeparamref name="TSearchObject"/>: a compatible instance passes through unchanged, anything else
    /// is converted by case-insensitive property copy. Unknown properties on the input are dropped.
    /// </summary>
    public static TSearchObject? Coerce<TSearchObject>(object? so)
        where TSearchObject : class, new()
        => so != null
            ? so as TSearchObject ?? ObjectUtility.Create<TSearchObject>(so)
            : null;
}
