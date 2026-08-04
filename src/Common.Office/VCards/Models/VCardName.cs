using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardName
{
    [VCardProperty("surname")]
    public string? SurName { get; set; }
    [VCardProperty("given")]
    public string? GivenName { get; set; }
    [VCardProperty("additional")]
    public string? Additional { get; set; }
    [VCardProperty("prefix")]
    public string? Prefix { get; set; }
    [VCardProperty("suffix")]
    public string? Suffix { get; set; }
}