namespace Regira.Invoicing.Peppol.Models.Abstractions;

public interface IInboxItem
{
    string Id { get; set; }
    string? Title { get; set; }
    string SenderId { get; set; }
    string ReceiverId { get; set; }
    string DocumentType { get; set; }
    Stream? Content { get; set; }
    DateTime Created { get; set; }
}