namespace Regira.Invoicing.Billit.Models.Peppol;

public class PeppolItem
{
    public long InboxItemId { get; set; }
    public string SenderPeppolId { get; set; } = null!;
    public string PeppolDocumentType { get; set; } = null!;
    public string ReceiverPeppolId { get; set; } = null!;
    public string ReceiverCompanyId { get; set; } = null!;
    public DateTime CreationDate { get; set; }
    public string PeppolFileId { get; set; } = null!;
    public PeppolSender? Sender { get; set; }
    public PeppolFile? File { get; set; }
}


public class PeppolSender
{
    public long PartyID { get; set; }
    public string Name { get; set; } = null!;
    public string VATNumber { get; set; } = null!;
}

public class PeppolFile
{
    public string FileID { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public bool? HasDocuments { get; set; }
}