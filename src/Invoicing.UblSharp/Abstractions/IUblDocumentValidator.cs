using System.Xml.Linq;
using Regira.Invoicing.UblSharp.Services;

namespace Regira.Invoicing.UblSharp.Abstractions;

public interface IUblDocumentValidator
{
    Task<UblDocumentResponse> Validate(XDocument input);
}