using Regira.Invoicing.Billit.Models.Contacts;

namespace Regira.Invoicing.Billit.Models.Orders.Input;

public class OrderInput
{
    public class OrderLineInput
    {
        public decimal Quantity { get; set; }
        public decimal UnitPriceExcl { get; set; } // Use decimal for currency/price fields
        public string? Description { get; set; }
        public string? DescriptionExtended { get; set; }
        public string? Reference { get; set; }
        public decimal VATPercentage { get; set; }
    }

    public OrderTypes OrderType { get; set; } = OrderTypes.Invoice;
    public OrderDirections OrderDirection { get; set; } = OrderDirections.Income;
    public string OrderNumber { get; set; } = null!;

    public DateOnly OrderDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string? PaymentReference { get; set; }

    public FileInput? OrderPDF { get; set; }
    public List<FileInput>? Attachments { get; set; }

    public CustomerInput Customer { get; set; } = null!;
    public List<OrderLineInput> OrderLines { get; set; } = null!;
    public List<AdditionalDocumentInput>? AdditionalDocumentReference { get; set; }
}

public class AdditionalDocumentInput
{
    public string? ID { get; set; }
    public string? DocumentDescription { get; set; }
    public string? DocumentType { get; set; }
    public FileInput File { get; set; } = null!;
}

public class FileInput
{
    public string? FileID { get; set; }
    public string FileName { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public byte[] FileContent { get; set; } = null!;
}