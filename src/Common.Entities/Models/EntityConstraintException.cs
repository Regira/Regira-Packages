namespace Regira.Entities.Models;

/// <summary>
/// A database integrity constraint (unique index, foreign key, NOT NULL, check) rejected the change.<br />
/// Thrown by the EFcore write services when <c>SaveChanges</c> fails on a constraint violation
/// (transient faults — deadlocks, timeouts — are not wrapped and keep surfacing as 500s).<br />
/// The web layers map it to HTTP 409 Conflict with <see cref="ClientMessage"/> as the response detail.
/// <see cref="Exception.Message"/> is the same generic text — safe to render anywhere (dev exception
/// pages, generic handlers); the provider's constraint message stays server-side, on
/// <see cref="Exception.InnerException"/> and in the write service's warning log.<br />
/// In an ASP.NET host the mapping is application-wide: <c>ConfigureDefaultJsonOptions()</c> registers
/// <c>EntityExceptionFilter</c>, so every MVC action answers 409 — generated, hand-written, or on a
/// controller of the consumer's own. The controller helpers (<c>ControllerExtensions.Save</c>/<c>Delete</c>)
/// and the <c>[EntityConstraintConflict]</c> attribute on the attachment controller bases catch it first and
/// emit the same body.<br />
/// A write surface <b>outside</b> MVC — a minimal endpoint, a background job, a message handler — is not
/// covered by that filter and leaks this as a 500 unless it maps the exception itself.
/// </summary>
public class EntityConstraintException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Generic detail returned to clients — provider messages can leak index names and other users' values.</summary>
    public const string ClientMessage = "A database constraint rejected the change.";
}
