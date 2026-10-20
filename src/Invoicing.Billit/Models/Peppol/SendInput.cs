using Regira.Invoicing.Billit.Models.Orders.Input;

namespace Regira.Invoicing.Billit.Models.Peppol;


[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public class PeppolSendInput
{
    public string TransportType { get; set; } = "Peppol";
    public OrderInput Order { get; set; } = null!;
}

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public class PeppolSendInputResponse
{
    public int? OrderID { get; set; } = null!;
}

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public class PeppolRefuseInput
{
    public string RefusalReason { get; set; } = "OTH";
}