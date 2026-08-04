namespace Office.Excel.Testing.Models;

public static class CountryExtensions
{
    public static IList<ExcelCountry> ToExcelCountries(this IEnumerable<Country> countries)
    {
        return countries.Select(c => c.ToExcelCountry()).ToList();
    }
    public static ExcelCountry ToExcelCountry(this Country country)
    {
        return new ExcelCountry
        {
            Name = country.Name,
            TopLevelDomain = string.Join(",", country.TopLevelDomain ?? new List<string>()),
            Alpha2Code = country.Alpha2Code,
            Alpha3Code = country.Alpha3Code,
            CallingCodes = string.Join(",", country.CallingCodes ?? new List<string>()),
            Latlng = string.Join(",", country.Latlng ?? new List<double>()),
            Area = country.Area,
            Population = country.Population,
            NumericCode = country.NumericCode,
            CreatedDate = DateTime.UtcNow
        };
    }
    public static Country ToCountry(this ExcelCountry item)
    {
        return new Country
        {
            Name = item.Name,
            TopLevelDomain = item.TopLevelDomain?.Split(',').ToList(),
            Alpha2Code = item.Alpha2Code,
            Alpha3Code = item.Alpha3Code,
            CallingCodes = item.CallingCodes?.Split(',').ToList(),
            Latlng = item.Latlng?.Split(',').Select(double.Parse).ToList(),
            Area = item.Area,
            Population = item.Population,
            NumericCode = item.NumericCode,
            CreatedDate = item.CreatedDate
        };
    }
}