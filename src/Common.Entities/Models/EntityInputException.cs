namespace Regira.Entities.Models;

/// <summary>
/// A write rejected because the client's input breaks a domain rule — <see cref="Errors"/> carries the field-level
/// messages, which the web layers return as the ModelState payload of an HTTP 400.
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
    private IList<EntityInputError> _errors = new List<EntityInputError>();
    private EntityInputErrorDictionary? _inputErrors;

    /// <summary>
    /// Every error, several per field allowed: the 400 response body is built from it, each message of a field its own entry.
    /// </summary>
    public IList<EntityInputError> Errors
    {
        get => _errors;
        set => _errors = new List<EntityInputError>(value ?? []);
    }
    /// <summary>
    /// Field name → message: <see cref="Errors"/> seen with one message per field, the messages of a field joined by a
    /// space. It is a view, not a second store — setting a field replaces that field's messages in <see cref="Errors"/>,
    /// and assigning a dictionary replaces them all.
    /// </summary>
    public IDictionary<string, string> InputErrors
    {
        get => _inputErrors ??= new EntityInputErrorDictionary(this);
        // materialized before it replaces the store: the value may be this very view
        set => _errors = (value ?? new Dictionary<string, string>()).Select(x => new EntityInputError(x.Key, x.Value)).ToList();
    }
}

/// <inheritdoc cref="EntityInputException"/>
/// <typeparam name="T">The entity whose write was rejected.</typeparam>
public class EntityInputException<T>(string message, Exception? innerException = null)
    : EntityInputException(message, innerException)
{
    public T? Item { get; set; }
}
