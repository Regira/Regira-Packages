using Regira.Invoicing.Billit.Models.Contacts;
using Regira.Invoicing.Billit.Models.Orders.Documents;

namespace Regira.Invoicing.Billit.Models.Orders.Dto;

public class OrderItemDto
{
    public int OrderId { get; set; }
    public int CompanyId { get; set; }
    public string OrderNumber { get; set; } = null!;

    public DateTime OrderDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    public OrderTypes OrderType { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime Created { get; set; }
    public OrderDirections OrderDirection { get; set; }

    public decimal TotalExcl { get; set; }
    public decimal TotalIncl { get; set; }
    public decimal TotalVat { get; set; }

    public string? PaymentReference { get; set; }
    public bool Paid { get; set; }
    public string? ExternalProvider { get; set; }
    public string Currency { get; set; } = null!;
    public bool IsSent { get; set; }
    public bool Invoiced { get; set; }
    public int RemindersSent { get; set; }
    public decimal ToPay { get; set; }
    public string OrderStatus { get; set; } = null!;
    public bool Overdue { get; set; }
    public int DaysOverdue { get; set; }
    public decimal FxRateToForeign { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool ExportedToConnector { get; set; }

    public OrderPdf? OrderPdf { get; set; }
    public CounterPartyDto CounterParty { get; set; } = null!;
    public DocumentDeliveryDetails? CurrentDocumentDeliveryDetails { get; set; }
}

public class OrderListResponse
{
    public List<OrderItemDto> Items { get; set; } = null!;
}