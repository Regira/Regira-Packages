using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VPhoto
{
    [VCardProperty("parameters/mediatype")]
    public string? MediaType { get; set; }// parameters/mediatype/text
    [VCardProperty("uri")]// as base64
    public byte[]? Bytes { get; set; }// uri
    [VCardProperty("uri")]
    public string? Uri { get; set; }// uri
}