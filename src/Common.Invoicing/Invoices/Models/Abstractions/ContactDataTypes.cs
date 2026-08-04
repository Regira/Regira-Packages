namespace Regira.Invoicing.Invoices.Models.Abstractions;

[Flags]
public enum ContactDataTypes
{
    Other = 0,
    Phone = 1 << 0,
    Email = 1 << 1,
    Website = 1 << 2
}