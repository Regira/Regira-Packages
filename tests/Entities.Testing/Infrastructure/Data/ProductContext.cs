using Microsoft.EntityFrameworkCore;
using Regira.DAL.EFcore.Extensions;

namespace Entities.Testing.Infrastructure.Data;

public class ProductContext(DbContextOptions<ProductContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ProductTag> ProductTags { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.SetUtcDateTimeConvention();
}
