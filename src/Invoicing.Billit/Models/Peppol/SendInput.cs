using Regira.Invoicing.Billit.Models.Orders.Input;

namespace Regira.Invoicing.Billit.Models.Peppol;


public class PeppolSendInput
{
    public string TransportType { get; set; } = "Peppol";
    public OrderInput Order { get; set; } = null!;
}

public class PeppolSendInputResponse
{
    public int? OrderID { get; set; } = null!;
}

public class PeppolRefuseInput
{
    public string RefusalReason { get; set; } = "OTH";
}