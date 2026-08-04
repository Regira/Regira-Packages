using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardTel
{
    [VCardProperty("parameters/type/text")]
    public VCardTelType? Type { get; set; }
    [VCardProperty("uri")]
    public string? Uri { get; set; }// format -> "tel: xxx"

    public static implicit operator VCardTel(string? tel) => new() { Uri = tel };
    public static implicit operator string?(VCardTel? value) => value?.Uri;
}