namespace Regira.Entities.Models;

/// <summary>
/// A database integrity constraint (unique index, foreign key, NOT NULL, check) rejected the change.<br />
/// Thrown by the EFcore write services when <c>SaveChanges</c> fails on a constraint violation
/// (transient faults — deadlocks, timeouts — are not wrapped and keep surfacing as 500s).<br />
/// The web layers map it to HTTP 409 Conflict with <see cref="ClientMessage"/> as the response detail.
/// <see cref="Exception.Message"/> is the same generic text — safe to render anywhere (dev exception
/// pages, generic handlers); the provider's constraint message stays server-side, on
/// <see cref="Exception.InnerException"/> and in the write service's warning log.<br />
/// <b>Every write surface must map this exception</b> — current mappings: the controller helpers
/// (<c>ControllerExtensions.Save</c>/<c>Delete</c>) and the <c>[EntityConstraintConflict]</c> exception filter
/// (attachment controller bases). A new write surface without a mapping leaks this as a 500.
/// </summary>
public class EntityConstraintException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Generic detail returned to clients — provider messages can leak index names and other users' values.</summary>
    public const string ClientMessage = "A database constraint rejected the change.";
}
