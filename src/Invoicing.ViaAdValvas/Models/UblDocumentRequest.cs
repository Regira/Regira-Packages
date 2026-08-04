namespace Regira.Invoicing.ViaAdValvas.Models;

public class UblDocumentRequest
{
    public string Channel { get; set; } = null!;
    public string DateTime { get; set; } = null!;
    public string? MessageReference { get; set; }
    public string ReceiverIdentification { get; set; } = null!;
    public string Seal { get; set; } = null!;
    public string SenderIdentification { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public string? ProcessType { get; set; }
    public int[]? Document { get; set; }
}