namespace Regira.Invoicing.UblSharp.Attributes;

internal class PeppolDescriptionAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}