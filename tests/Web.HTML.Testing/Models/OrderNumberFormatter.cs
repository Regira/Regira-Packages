namespace Web.HTML.Testing.Models;

public class OrderNumberFormatter
{
    public string Format(Order order) => $"ORD-{order.Id}";
}
