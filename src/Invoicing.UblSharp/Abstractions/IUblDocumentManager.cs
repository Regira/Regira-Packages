namespace Regira.Invoicing.UblSharp.Abstractions;

[Obsolete("No package implements it, and IUblDocumentValidator is obsolete: send with Regira.Invoicing.ViaAdValvas' PeppolService.")]
public interface IUblDocumentManager : IUblDocumentSender, IUblDocumentValidator
{
}