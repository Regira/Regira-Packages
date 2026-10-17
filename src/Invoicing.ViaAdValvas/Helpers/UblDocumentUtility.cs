using System.Xml.Linq;

namespace Regira.Invoicing.ViaAdValvas.Helpers;

[Obsolete("Use Regira.Invoicing.Peppol.UblDocumentUtility from Regira.Invoicing. Will be removed in v7.")]
public static class UblDocumentUtility
{
    extension(XDocument doc)
    {
        public string? GetReferenceId() => Peppol.UblDocumentUtility.GetReferenceId(doc);
        public string? GetRecipientId() => Peppol.UblDocumentUtility.GetRecipientId(doc);
        public string? GetRecipientSchemeId() => Peppol.UblDocumentUtility.GetRecipientSchemeId(doc);
    }
}
