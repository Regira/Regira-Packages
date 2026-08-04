namespace Office.Csv.Testing.Infrastructure;

public class CsvProduct
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public decimal Price { get; set; }
    public double? Quantity { get; set; }
    public bool IsOnline { get; set; }
    public string? Description { get; set; }
    public DateTime Created { get; set; }
}