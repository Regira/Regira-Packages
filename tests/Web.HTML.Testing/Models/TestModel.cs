namespace Web.HTML.Testing.Models;

public class Order
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? ImgBase64 { get; set; }
    public DateTime Created { get; set; }
    public ICollection<OrderLine>? OrderLines { get; set; }
}