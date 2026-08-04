namespace Regira.Invoicing.Billit.Models.Peppol;

public class InboxResponse
{
    public IList<InboxItemDto> InboxItems { get; set; } = null!;
}

public class InboxItemDto
{
    public long InboxItemID { get; set; }
    public string SenderPeppolID { get; set; } = null!;
    public string PeppolDocumentType { get; set; } = null!;
    public string ReceiverPeppolID { get; set; } = null!;
    public string ReceiverCompanyID { get; set; } = null!;
    public DateTime CreationDate { get; set; }
    public string PeppolFileID { get; set; } = null!;
}