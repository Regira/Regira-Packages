namespace Regira.Office.Word.Models;

public record WordTable
{
    public string? Name { get; set; }
    public string[,]? Data { get; set; }// = { { "a", "b", "c" }, { "a2", "b2", "c2" } };
    public double[]? Width { get; set; }

    public bool Border { get; set; }
}