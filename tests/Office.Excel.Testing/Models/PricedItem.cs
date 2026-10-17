using System.Globalization;

namespace Office.Excel.Testing.Models;

public class PricedItem
{
    public string? Title { get; set; }
    public Money? Price { get; set; }
}

public record Money(decimal Amount, string Currency)
{
    public override string ToString() => $"{Amount.ToString(CultureInfo.InvariantCulture)} {Currency}";
}
