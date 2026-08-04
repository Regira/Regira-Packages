namespace Office.Excel.Testing.Models;

public class Country
{
    public string? Name { get; set; }
    public List<string>? TopLevelDomain { get; set; }
    public string? Alpha2Code { get; set; }
    public string? Alpha3Code { get; set; }
    public List<string>? CallingCodes { get; set; }
    public List<double>? Latlng { get; set; }
    public double? Area { get; set; }
    public int? Population { get; set; }
    public string? NumericCode { get; set; }
    public DateTime? CreatedDate { get; set; }
}