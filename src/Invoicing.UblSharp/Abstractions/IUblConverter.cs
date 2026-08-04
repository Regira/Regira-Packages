using System.Xml.Linq;
using Regira.Invoicing.UblSharp.Core;

namespace Regira.Invoicing.UblSharp.Abstractions;

public interface IUblConverter
{
    XDocument Convert(UblDocumentInput input);
}