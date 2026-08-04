namespace Regira.Payments.Pom;

public class PomPayment
{
    public string? Id { get; set; }
    public string? SenderContractNumber { get; set; }
    public string? DocumentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime DocumentDate { get; set; } = DateTime.Now;
    public string DocumentType { get; set; } = "Contribution";
    public string? CommunicationStructured { get; set; }// optional
    public string? CommunicationPart1 { get; set; }
    public string? CustomerId { get; set; } // -> email?
    public string? Email { get; set; }// optional
    public DateTime ExpiryDate { get; set; } = DateTime.Now.AddDays(1);
    public string Language { get; set; } = "nl_BE";
    public string? PaymentStatus { get; set; }
}