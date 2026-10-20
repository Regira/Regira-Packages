using System.Xml.Linq;

namespace Regira.Invoicing.UblSharp.Utilities;

[Obsolete("Use Regira.Invoicing.Peppol.UblDocumentUtility from Regira.Invoicing. Will be removed in v7.")]
public static class UblDocumentUtility
{
    public static string? GetReferenceId(XDocument doc) => Peppol.UblDocumentUtility.GetReferenceId(doc);
    public static string? GetRecipientId(XDocument doc) => Peppol.UblDocumentUtility.GetRecipientId(doc);
    public static string? GetRecipientSchemeId(XDocument doc) => Peppol.UblDocumentUtility.GetRecipientSchemeId(doc);
}
