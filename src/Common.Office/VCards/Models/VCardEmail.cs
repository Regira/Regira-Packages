using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardEmail
{
    [VCardProperty("parameters/type/text")]
    public VCardPropertyType? Type { get; set; }
    [VCardProperty("text")]
    public string? Text { get; set; }

    public static implicit operator VCardEmail(string? email) => new() { Text = email };
    public static implicit operator string?(VCardEmail? value) => value?.Text;
}