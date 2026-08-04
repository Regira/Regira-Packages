namespace Regira.Invoicing.UblSharp.Attributes;

internal class PeppolListAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}