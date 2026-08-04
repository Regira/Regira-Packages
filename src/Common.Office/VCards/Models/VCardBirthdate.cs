using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public record VCardBirthdate
{
    [VCardProperty("date")]
    public DateTime? Date { get; set; }
    [VCardProperty("text")]
    public string? Text { get; set; }

    public static implicit operator VCardBirthdate(DateTime date) => new() { Date = date };
    public static implicit operator DateTime?(VCardBirthdate value) => value?.Date;
}