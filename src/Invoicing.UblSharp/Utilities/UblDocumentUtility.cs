using System.Xml.Linq;
using Regira.Invoicing.UblSharp.Core;

namespace Regira.Invoicing.UblSharp.Utilities;

public static class UblDocumentUtility
{
    private static readonly XNamespace cac = PeppolNamespaces.CAC;
    private static readonly XNamespace cbc = PeppolNamespaces.CBC;

    public static string? GetReferenceId(XDocument doc)
    {
        return doc.Root
            ?.Element(cac + "OrderReference")
            ?.Element(cbc + "ID")
            ?.Value;
    }
    public static string? GetRecipientId(XDocument doc)
    {
        return doc.Root
            ?.Element(cac + "AccountingCustomerParty")
            ?.Element(cac + "Party")
            ?.Element(cbc + "EndpointID")
            ?.Value;
    }
    public static string? GetRecipientSchemeId(XDocument doc)
    {
        return doc.Root
            ?.Element(cac + "AccountingCustomerParty")
            ?.Element(cac + "Party")
            ?.Element(cbc + "EndpointID")
            ?.Attribute("schemeID")
            ?.Value;
    }
}