namespace Regira.Invoicing.UblSharp.Attributes;

internal class PeppolCodeAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}