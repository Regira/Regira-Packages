using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardAddress
{
    [VCardProperty("parameters/label/text")]
    public string? Label { get; set; }
    [VCardProperty("parameters/type/text")]
    public VCardPropertyType? Type { get; set; }
    [VCardProperty("pobox")]
    public string? PostBox { get; set; }
    [VCardProperty("ext")]
    public string? Extension { get; set; }
    [VCardProperty("street")]
    public string? StreetAndNumber { get; set; }
    [VCardProperty("locality")]
    public string? Locality { get; set; }
    [VCardProperty("region")]
    public string? Region { get; set; }
    [VCardProperty("code")]
    public string? PostalCode { get; set; }
    [VCardProperty("country")]
    public string? Country { get; set; }
}