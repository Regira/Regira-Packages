# Office.CSV — Example: Customer Import / Export

> Context: A CRM allows admins to import customers from a CSV file and export filtered results.

## Import customers from an uploaded CSV

<!-- no-compile -->
```csharp
public async Task<List<Customer>> ImportCustomers(IFormFile file)
{
    var binary = file.ToNamedFile().ToBinaryFile();

    var rows = await new CsvManager<CustomerImportDto>()
        .Read(binary, new CsvHelperOptions { Delimiter = ";" });

    return rows.Select(r => new Customer
    {
        Name  = r.Name,
        Email = r.Email,
        Phone = r.Phone
    }).ToList();
}
```

`CustomerImportDto`:
```csharp
using CsvHelper.Configuration.Attributes;   // [Name] — CsvHelper, a dependency of Regira.Office.Csv.CsvHelper

public class CustomerImportDto
{
    [Name("Name")]  public string? Name  { get; set; }
    [Name("Email")] public string? Email { get; set; }
    [Name("Phone")] public string? Phone { get; set; }
}
```

## Export customers to a downloadable CSV

<!-- no-compile -->
```csharp
public async Task<IMemoryFile> ExportCustomers(IEnumerable<Customer> customers)
    => await new CsvManager<CustomerExportDto>()
        .WriteFile(
            customers.Select(c => new CustomerExportDto
            {
                Name        = c.Name,
                Email       = c.Email,
                CreatedDate = c.Created.ToString("yyyy-MM-dd")
            }),
            new CsvOptions { Delimiter = "," }
        );
```

## Controller action

<!-- no-compile -->
```csharp
[HttpGet("export")]
public async Task<IActionResult> Export()
{
    var customers = await _customerService.List();
    var file      = await _exportService.ExportCustomers(customers);
    return this.File(file.ToBinaryFile("customers.csv"));   // File() takes an INamedFile: the name is the download's file name
}
```
