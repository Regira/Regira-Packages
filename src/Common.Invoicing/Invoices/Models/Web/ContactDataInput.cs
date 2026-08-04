using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Models.Web;

public class ContactDataInput
{
    public string Value { get; set; } = null!;
    public ContactDataTypes DataType { get; set; }
}