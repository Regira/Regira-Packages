# Quickstart — a CRUD API in five minutes

This walkthrough goes from an empty folder to a working entity CRUD/REST API on the Regira Entities stack. It stays inside the free tier (two simple registrations; the limit is 5 simple + 2 complex — see [licensing](../licensing.md)).

> Prefer AI-assisted setup? Connect the [MCP server](../README.md#connect-the-mcp-server-recommended) and ask your agent to scaffold this instead — `get_bootstrap_guide` serves the full workflow.

## 1. Create the project and install packages

```sh
dotnet new webapi -n Shop.Api
cd Shop.Api
dotnet add package Regira.Entities.Web
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

`Regira.Entities.Web` transitively brings `Regira.Entities`, `Regira.Entities.DependencyInjection`, and `Regira.Entities.EFcore`.

## 2. Define entities and a DbContext

Entities are plain classes implementing `IEntity<TKey>`:

```csharp
using Regira.Entities.Models.Abstractions;

public class Category : IEntity<int>
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
}

public class Product : IEntity<int>
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}
```

The DbContext stays a plain EF Core context — no Regira calls required in it:

```csharp
using Microsoft.EntityFrameworkCore;

public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
}
```

## 3. Wire it up in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddDbContext<ShopContext>(db => db.UseSqlite("Data Source=shop.db"))
    .UseRegira(builder.Configuration)                  // license keys from Regira:LicenseKeys; free tier without
    .UseEntities<ShopContext>(o => o.UseDefaults())    // interceptors + conventions auto-wired
    .For<Category>()
    .For<Product>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

## 4. Add controllers

One line per entity — `EntityControllerBase` registers list, details, search, save, and remove endpoints with filtering, sorting, and paging:

```csharp
using Microsoft.AspNetCore.Mvc;
using Regira.Entities.Web.Controllers;

[ApiController, Route("categories")]
public class CategoryController : EntityControllerBase<Category>;

[ApiController, Route("products")]
public class ProductController : EntityControllerBase<Product>;
```

## 5. Run it

```sh
dotnet run
```

Your API now serves CRUD endpoints at `/categories` and `/products`. Add the OpenAPI UI of your choice to explore them, or start from the runnable reference implementation in this repo — [`tests/Entities.TestApi`](../tests/Entities.TestApi) — which adds search objects, sorting/include enums, DTO mapping, attachments, and the Scalar UI.

## Where next

- [Entity models](../src/Common.Entities/docs/models.md) — keys, timestamps, archiving, normalization
- [Web endpoints](../src/Common.Entities/docs/web-endpoints.md) — the generated API surface, paging defaults
- [Practical examples](../src/Common.Entities/docs/examples.md) — a full webshop scenario
- [Samples & demos](../README.md#samples--demos) — sample repos and live apps built on these packages
- [Licensing](../licensing.md) — what is free, what needs a key
