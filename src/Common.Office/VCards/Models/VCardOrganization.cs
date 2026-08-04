using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardOrganization
{
    [VCardProperty("parameters/type/text")]
    public VCardPropertyType Type { get; set; }
    [VCardProperty("text")]
    public string? Name { get; set; }

    public static implicit operator VCardOrganization(string? name) => new() { Name = name };
    public static implicit operator string?(VCardOrganization? org) => org?.Name;
}