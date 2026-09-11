namespace Regira.Entities.Models;

/// <summary>
/// The change was built on a stale read: the row no longer holds the concurrency token the client sent, or another
/// writer removed it since it was read.<br />
/// Thrown by the EFcore write services when <c>SaveChanges</c> fails with EF Core's
/// <c>DbUpdateConcurrencyException</c>, which stays on <see cref="Exception.InnerException"/> — its <c>Entries</c>
/// name the rows that conflicted.<br />
/// The web layers map it to HTTP 409 Conflict with <see cref="ClientMessage"/> as the response detail, under a
/// different title than <see cref="EntityConstraintException"/>'s 409: this one tells the client to reload and try
/// again, that one that the input itself was rejected.<br />
/// In an ASP.NET host the mapping is application-wide: <c>ConfigureDefaultJsonOptions()</c> registers
/// <c>EntityExceptionFilter</c>, so every MVC action answers 409 — generated, hand-written, or on a
/// controller of the consumer's own. The controller helpers (<c>ControllerExtensions.Save</c>/<c>Delete</c>)
/// and the <c>[EntityConstraintConflict]</c> attribute on the attachment controller bases catch it first and
/// emit the same body.<br />
/// A write surface <b>outside</b> MVC — a minimal endpoint, a background job, a message handler — is not
/// covered by that filter and leaks this as a 500 unless it maps the exception itself.
/// </summary>
public class EntityConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Generic detail returned to clients.</summary>
    public const string ClientMessage = "The record was changed or removed since it was read. Reload it and try again.";
}
