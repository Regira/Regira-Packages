namespace Regira.Entities.Models;

/// <summary>
/// A row the write needs does not exist for the caller: one a validator, a prepper or a request handler looked up and
/// did not find, such as a parent the input refers to.<br />
/// The web layers map it to HTTP 404 Not Found, a problem whose detail is <see cref="Exception.Message"/>, so write the
/// message for the client; the parameterless constructor uses <see cref="ClientMessage"/>. The mapping is the one that
/// answers <see cref="EntityInputException"/> with 400: <c>EntityExceptionFilter</c> and the controller bases'
/// <c>[EntityConstraintConflict]</c> in MVC, <c>EntityExceptionEndpointFilter</c> on the mapped endpoints.
/// </summary>
public class EntityNotFoundException(string message = EntityNotFoundException.ClientMessage, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The default detail returned to clients.</summary>
    public const string ClientMessage = "The record was not found.";
}
