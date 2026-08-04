using Regira.Invoicing.UblSharp.Attributes;

namespace Regira.Invoicing.UblSharp.Core;

// https://docs.peppol.eu/pracc/catalogue/1.0/codelist/UNCL5305/
// https://docs.peppol.eu/poacc/billing/3.0/codelist/UNCL5305/
[PeppolList("UNCL5305")]
public enum TaxCategoryCode
{
    [PeppolCode("AE"), PeppolDescription("Code specifying that the standard VAT rate is levied from the invoicee.")]
    Reverse,
    [PeppolCode("E"), PeppolDescription("Code specifying that taxes are not applicable.")]
    Exempt,
    [PeppolCode("S"), PeppolDescription("Code specifying the standard rate.")]
    Standard,
    [PeppolCode("Z"), PeppolDescription("Code specifying that the goods are at a zero rate.")]
    Zero,
    [PeppolCode("H"), PeppolDescription("Code specifying a higher rate of duty or tax or fee.")]
    HigherRate,
    [PeppolCode("AA"), PeppolDescription("Tax rate is lower than standard rate.")]
    LowerRate,
    [PeppolCode("G"), PeppolDescription("Code specifying that the item is free export and taxes are not charged.")]
    Export,
    [PeppolCode("O"), PeppolDescription("Code specifying that taxes are not applicable to the services.")]
    OutsideScode,
    [PeppolCode("K"), PeppolDescription("A tax category code indicating the item is VAT exempt due to an intra-community supply in the European Economic Area.")]
    IntraCommunity,
    [PeppolCode("L"), PeppolDescription("Impuesto General Indirecto Canario (IGIC) is an indirect tax levied on goods and services supplied in the Canary Islands (Spain) by traders and professionals, as well as on import of goods.")]
    CanaryIslands,
    [PeppolCode("M"), PeppolDescription("Impuesto sobre la Producción, los Servicios y la Importación (IPSI) is an indirect municipal tax, levied on the production, processing and import of all kinds of movable tangible property, the supply of services and the transfer of immovable property located in the cities of Ceuta and Melilla.")]
    CeutaAndMelilla
}