namespace Regira.Invoicing.UblSharp.Exceptions;

public class UblRemoteException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string? Mode { get; set; }
    public string? Url { get; set; }
    public int ServiceStatusCode { get; set; }
    public string? SerializedRequest { get; set; }
    public string? ResponseContent { get; set; }
}