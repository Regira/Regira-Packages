using Regira.Invoicing.Peppol.Models.Abstractions;

namespace Regira.Invoicing.Peppol.Models;

public class InboxItem : IInboxItem
{
    public string Id { get; set; } = null!;
    public string? Title { get; set; }
    public string SenderId { get; set; } = null!;
    public string ReceiverId { get; set; } = null!;
    public string DocumentType { get; set; } = null!;
    public Stream? Content { get; set; }
    public DateTime Created { get; set; }
}