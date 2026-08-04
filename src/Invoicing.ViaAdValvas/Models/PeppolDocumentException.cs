namespace Regira.Invoicing.ViaAdValvas.Models;

public class PeppolResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int ServiceStatusCode { get; set; }
    public string? SerializedResponse { get; set; }
}