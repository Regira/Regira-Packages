namespace Regira.Entities.Models;

/// <summary>
/// A write rejected because the client's input breaks a domain rule — <see cref="InputErrors"/> carries the
/// field-level messages, which the web layers return as the ModelState payload of an HTTP 400.
/// <para>
/// Throw the generic <see cref="EntityInputException{T}"/>; this base exists so a handler can catch every
/// input rejection whatever entity it was parameterized with. A <c>catch</c> on one closed generic (what the
/// generated write actions do for their own <c>TEntity</c>) misses the one a prepper threw for a related
/// entity — the reason the ASP.NET exception filter matches this type instead.
/// </para>
/// </summary>
public abstract class EntityInputException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Field name → message, echoed verbatim into the 400 response body.</summary>
    public IDictionary<string, string> InputErrors { get; set; } = new Dictionary<string, string>();
}

/// <inheritdoc cref="EntityInputException"/>
/// <typeparam name="T">The entity whose write was rejected.</typeparam>
public class EntityInputException<T>(string message, Exception? innerException = null)
    : EntityInputException(message, innerException)
{
    public T? Item { get; set; }
}
