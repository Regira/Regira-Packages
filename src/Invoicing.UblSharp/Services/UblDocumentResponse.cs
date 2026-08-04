namespace Regira.Invoicing.UblSharp.Services;

public class UblDocumentResponse
{
    public string? Reference { get; set; }
    public string? Mode { get; set; }
    public string? Action { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? SerializedRequest { get; set; }
}