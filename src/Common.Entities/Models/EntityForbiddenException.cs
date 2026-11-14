namespace Regira.Entities.Models;

/// <summary>
/// The caller may not make this write: a rule about who may change a row, enforced in a validator, a prepper or a
/// request handler.<br />
/// The web layers map it to HTTP 403 Forbidden, a problem whose detail is <see cref="Exception.Message"/>, so write the
/// message for the client; the parameterless constructor uses <see cref="ClientMessage"/>. The mapping is the one that
/// answers <see cref="EntityInputException"/> with 400: <c>EntityExceptionFilter</c> and the controller bases'
/// <c>[EntityConstraintConflict]</c> in MVC, <c>EntityExceptionEndpointFilter</c> on the mapped endpoints.<br />
/// A row the caller may not even see belongs out of the query, in a global filter: its writes then answer 404 by
/// themselves. Throw this for a row the caller may read but not change.
/// </summary>
public class EntityForbiddenException(string message = EntityForbiddenException.ClientMessage, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The default detail returned to clients.</summary>
    public const string ClientMessage = "You may not change this record.";
}
