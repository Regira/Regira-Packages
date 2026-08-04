using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardGender
{
    [VCardProperty("sex")]
    public VCardGenderSex? Sex { get; set; }
    [VCardProperty("identity")]
    public string? Identity { get; set; }

    public static implicit operator VCardGender(VCardGenderSex sex) => new() { Sex = sex };
    public static implicit operator VCardGender(string value) => Enum.TryParse(value, out VCardGenderSex sex) ? sex : new();
    public static implicit operator VCardGenderSex?(VCardGender? value) => value?.Sex;
    public static implicit operator string?(VCardGender? value) => value?.Sex?.ToString();
}