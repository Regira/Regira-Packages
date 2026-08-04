using Microsoft.EntityFrameworkCore;
using Regira.Entities.EFcore.Extensions;

namespace Entities.Providers.Testing.Infrastructure;

public class WidgetContext(DbContextOptions<WidgetContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // hides archived rows on every provider — the query builders only translate the opt-ins
        modelBuilder.SetArchivedQueryFilter();
    }
}
