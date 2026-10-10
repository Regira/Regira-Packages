using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Utilities;

namespace Entities.Mediator.Testing.Infrastructure;

public enum ProductKind { Standard, Refill }

public class Product : IEntity<int>
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int Stock { get; set; }
    public ProductKind Kind { get; set; }
}

/// <summary>
/// <see cref="Title"/> is a non-nullable string without <c>[Required]</c>: MVC's model validation refuses it null,
/// DataAnnotations alone do not.
/// </summary>
public class ProductInputDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    [Range(0, 1000)]
    public int Stock { get; set; }
    public ProductKind Kind { get; set; }
}

public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

/// <summary>Top-level and public because MVC only discovers controllers that are.</summary>
[Route("products")]
public class ProductsController : EntityControllerBase<Product, Product, ProductInputDto>;

/// <summary>The DTOs share the entity's property names, so a JSON round trip is an exact mapper.</summary>
public sealed class JsonMapper : IEntityMapper
{
    public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
    public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
}

/// <summary>
/// One product, <c>Pen</c>, in an in-memory SQLite database behind <c>For&lt;Product&gt;()</c>, whose validator refuses the
/// title <c>refused</c> — as a service provider or as an MVC host over HTTP.
/// </summary>
public sealed class Shop : IAsyncDisposable
{
    public const string RefusedTitle = "refused";

    private readonly SqliteConnection _connection;
    private readonly WebApplication? _app;

    private Shop(SqliteConnection connection, IServiceProvider services, WebApplication? app, int penId)
    {
        _connection = connection;
        _app = app;
        Services = services;
        PenId = penId;
        Client = app?.GetTestClient();
    }

    public IServiceProvider Services { get; }
    public HttpClient? Client { get; }
    public int PenId { get; }

    /// <summary>The services alone, without MVC: what a job or a worker host has.</summary>
    public static async Task<Shop> CreateServicesAsync(Action<IServiceCollection>? services = null, Action<EntityServiceCollectionOptions>? entities = null)
    {
        var connection = OpenConnection();
        var collection = new ServiceCollection();
        collection.AddLogging();
        Register(collection, connection, entities);
        services?.Invoke(collection);
        var provider = collection.BuildServiceProvider();
        var penId = await SeedAsync(provider);
        return new Shop(connection, provider, null, penId);
    }

    /// <summary>An MVC host serving <see cref="ProductsController"/> over a test server.</summary>
    public static async Task<Shop> CreateHostAsync(Action<IServiceCollection>? services = null, Action<EntityServiceCollectionOptions>? entities = null)
    {
        var connection = OpenConnection();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProductsController).Assembly);
        Register(builder.Services, connection, entities);
        services?.Invoke(builder.Services);

        var app = builder.Build();
        app.MapControllers();
        var penId = await SeedAsync(app.Services);
        await app.StartAsync();
        return new Shop(connection, app.Services, app, penId);
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        return connection;
    }

    private static void Register(IServiceCollection services, SqliteConnection connection, Action<EntityServiceCollectionOptions>? entities)
    {
        services.AddDbContext<ShopContext>(db => db.UseSqlite(connection));
        services.AddSingleton<IEntityMapper, JsonMapper>();
        services
            .UseEntities<ShopContext>(o =>
            {
                o.UseDefaults();
                entities?.Invoke(o);
            })
            .For<Product>(e => e.Validate(ctx =>
            {
                if (ctx.Item.Title == RefusedTitle)
                {
                    ctx.AddError(nameof(Product.Title), "Refused.");
                }
            }));
    }

    private static async Task<int> SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        await db.Database.EnsureCreatedAsync();
        var pen = new Product { Title = "Pen", Stock = 5 };
        db.Products.Add(pen);
        await db.SaveChangesAsync();
        return pen.Id;
    }

    public async Task<Product?> FindAsync(int id)
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ShopContext>().Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
        else if (Services is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
        _connection.Close();
    }
}
