using Regira.Invoicing.UblSharp.Attributes;

namespace Regira.Invoicing.UblSharp.Core;

[PeppolList("UNCL1001")]
public enum InvoiceTypeCode
{
    [PeppolCode("80")]
    GoodsOrServices,
    [PeppolCode("82")]
    MeteredServices,
    [PeppolCode("84")]
    FinancialAdjustments,
    [PeppolCode("380")]
    Commercial,
    [PeppolCode("383")]
    DebitNote,
    [PeppolCode("386")]
    Prepayment,
    [PeppolCode("393")]
    Factored,
    [PeppolCode("395")]
    Consignment,
    [PeppolCode("575")]
    Insurer,
    [PeppolCode("623")]
    Forwarder,
    [PeppolCode("780")]
    Freight
}