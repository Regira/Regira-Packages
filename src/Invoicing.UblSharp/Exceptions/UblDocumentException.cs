namespace Regira.Invoicing.UblSharp.Exceptions;

public class UblDocumentException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string? Mode { get; set; }
    public int ServiceStatusCode { get; set; }
    public string? SerializedRequest { get; set; }
    public string? SerializedResponse { get; set; }
}