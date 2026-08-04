using System.Xml.Linq;
using Regira.Invoicing.ViaAdValvas.Config;

namespace Regira.Invoicing.ViaAdValvas.Helpers;

public static class UblDocumentUtility
{
    private static readonly XNamespace cac = PeppolNamespaces.CAC;
    private static readonly XNamespace cbc = PeppolNamespaces.CBC;

    extension(XDocument doc)
    {
        public string? GetReferenceId()
        {
            return doc.Root
                ?.Element(cac + "OrderReference")
                ?.Element(cbc + "ID")
                ?.Value;
        }

        public string? GetRecipientId()
        {
            return doc.Root
                ?.Element(cac + "AccountingCustomerParty")
                ?.Element(cac + "Party")
                ?.Element(cbc + "EndpointID")
                ?.Value;
        }

        public string? GetRecipientSchemeId()
        {
            return doc.Root
                ?.Element(cac + "AccountingCustomerParty")
                ?.Element(cac + "Party")
                ?.Element(cbc + "EndpointID")
                ?.Attribute("schemeID")
                ?.Value;
        }
    }
}