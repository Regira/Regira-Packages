namespace Regira.Payments.Pom;

public class PomPaymentResponse
{
    public string? PomDocumentId { get; set; }
    public string? DocumentId { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Paylink { get; set; }
}