namespace Regira.Invoicing.Billit.Models.Orders.Input;

public class OrderSearchObject
{
    public OrderTypes? OrderType { get; set; }
    public OrderDirections? OrderDirection { get; set; }
}