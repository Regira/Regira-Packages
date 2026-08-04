# Office.Excel — Example: Order Report Export

> Context: A sales dashboard lets managers download monthly order reports as Excel files, and import product price lists uploaded by suppliers.

## Export orders to Excel

```csharp
public async Task<IMemoryFile> ExportOrders(IEnumerable<Order> orders)
{
    IExcelService<OrderRow> excel = new Regira.Office.Excel.MiniExcel.ExcelManager<OrderRow>();

    var sheet = new ExcelSheet<OrderRow>
    {
        Name = "Orders",
        Data = orders.Select(o => new OrderRow
        {
            OrderId      = o.Id,
            CustomerName = o.CustomerName,
            Total        = o.Total,
            Date         = o.OrderDate.ToString("yyyy-MM-dd"),
            Status       = o.Status.ToString()
        }).ToList()
    };

    return await excel.Create([sheet]);
}

public class OrderRow
{
    public int     OrderId      { get; set; }
    public string? CustomerName { get; set; }
    public decimal Total        { get; set; }
    public string? Date         { get; set; }
    public string? Status       { get; set; }
}
```

## Import supplier price list

```csharp
public async Task<IEnumerable<PriceUpdate>> ImportPriceList(byte[] excelBytes)
{
    IExcelService<PriceUpdate> excel = new Regira.Office.Excel.MiniExcel.ExcelManager<PriceUpdate>();
    var file   = excelBytes.ToBinaryFile();
    var sheets = await excel.Read(file);
    return sheets.FirstOrDefault()?.Data ?? [];
}

public class PriceUpdate
{
    public string?  Sku   { get; set; }
    public decimal  Price { get; set; }
}
```

## Controller action

```csharp
[HttpGet("orders/export")]
public async Task<IActionResult> DownloadOrderReport()
{
    var orders = _orderService.List();
    var file   = await _reportService.ExportOrders(orders);
    return this.File(file);
}
```
