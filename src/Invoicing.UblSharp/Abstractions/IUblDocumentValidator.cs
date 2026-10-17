using System.Xml.Linq;
using Regira.Invoicing.UblSharp.Services;

namespace Regira.Invoicing.UblSharp.Abstractions;

[Obsolete("No package implements it: validate a document with a Peppol validator of your choice.")]
public interface IUblDocumentValidator
{
    Task<UblDocumentResponse> Validate(XDocument input);
}