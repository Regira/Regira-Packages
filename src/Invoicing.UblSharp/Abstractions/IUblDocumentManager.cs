namespace Regira.Invoicing.UblSharp.Abstractions;

[Obsolete("No package implements it, and IUblDocumentValidator is obsolete: use IUblDocumentSender.")]
public interface IUblDocumentManager : IUblDocumentSender, IUblDocumentValidator
{
}