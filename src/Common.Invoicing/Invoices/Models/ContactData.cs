using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Models;

public class ContactData
{
    public string Value { get; set; } = null!;
    public ContactDataTypes DataType { get; set; }
}