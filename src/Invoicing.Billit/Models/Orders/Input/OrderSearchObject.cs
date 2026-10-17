namespace Regira.Invoicing.Billit.Models.Orders.Input;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public class OrderSearchObject
{
    public OrderTypes? OrderType { get; set; }
    public OrderDirections? OrderDirection { get; set; }
}