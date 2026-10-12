using System.Xml.Linq;

namespace Regira.Invoicing.Peppol;

/// <summary>Reads the order reference and the recipient of a UBL invoice or credit note.</summary>
public static class UblDocumentUtility
{
    private static readonly XNamespace Cac = PeppolNamespaces.CAC;
    private static readonly XNamespace Cbc = PeppolNamespaces.CBC;

    extension(XDocument doc)
    {
        public string? GetReferenceId()
            => doc.Root
                ?.Element(Cac + "OrderReference")
                ?.Element(Cbc + "ID")
                ?.Value;

        public string? GetRecipientId()
            => RecipientEndpoint(doc)?.Value;

        public string? GetRecipientSchemeId()
            => RecipientEndpoint(doc)?.Attribute("schemeID")?.Value;
    }

    private static XElement? RecipientEndpoint(XDocument doc)
        => doc.Root
            ?.Element(Cac + "AccountingCustomerParty")
            ?.Element(Cac + "Party")
            ?.Element(Cbc + "EndpointID");
}
