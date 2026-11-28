using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Validators;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators.Abstractions;

namespace Entities.Testing;

/// <summary>
/// An app-wide write service set with <c>UseWriteService()</c>: every save through an entity service passes through it,
/// for the int-keyed and the keyed <c>For&lt;&gt;()</c> shapes alike, and the validators still run.
/// </summary>
[TestFixture]
public class AppWriteServiceOverrideTests
{
    public class Ticket : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }
    public class Label : IEntity<Guid>
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
    }

    public class TicketContext(DbContextOptions<TicketContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<Label> Labels => Set<Label>();
    }

    public class CountingWriteService<TContext, TEntity, TKey>(
        TContext dbContext,
        IEntityReadService<TEntity, TKey> readService,
        IEnumerable<IEntityPrepper> preppers,
        IEnumerable<IEntityValidator> validators,
        ILoggerFactory? loggerFactory = null)
        : EntityWriteService<TContext, TEntity, TKey>(dbContext, readService, preppers, validators, loggerFactory)
        where TContext : DbContext
        where TEntity : class, IEntity<TKey>
    {
        public static readonly List<string> Calls = [];

        public override Task Save(TEntity item, CancellationToken token = default)
        {
            lock (Calls)
            {
                Calls.Add($"Save({typeof(TEntity).Name})");
            }
            return base.Save(item, token);
        }
    }

    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        CountingWriteService<TicketContext, Ticket, int>.Calls.Clear();
        CountingWriteService<TicketContext, Label, Guid>.Calls.Clear();
    }

    [TearDown]
    public void TearDown() => _connection.Close();

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TicketContext>(db => db.UseSqlite(_connection));
        services.UseEntities<TicketContext>(o => o
                .UseDefaults()
                .UseWriteService(typeof(CountingWriteService<,,>))
                .AddValidator<Ticket>(ctx =>
                {
                    if (string.IsNullOrEmpty(ctx.Item.Title))
                    {
                        ctx.AddError(nameof(Ticket.Title), "A title is required.");
                    }
                }))
            .For<Ticket>()
            .For<Label, Guid>();
        return services.BuildServiceProvider();
    }

    [Test]
    public async Task Saves_Of_Every_Shape_Pass_Through_The_App_Write_Service()
    {
        await using var sp = Build();
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TicketContext>().Database.EnsureCreatedAsync();

        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket>>();
        await tickets.Save(new Ticket { Title = "First" });
        await tickets.SaveChanges();
        var labels = scope.ServiceProvider.GetRequiredService<IEntityService<Label, Guid>>();
        await labels.Save(new Label { Title = "Urgent" });
        await labels.SaveChanges();

        Assert.That(CountingWriteService<TicketContext, Ticket, int>.Calls, Is.EqualTo(new[] { "Save(Ticket)" }));
        Assert.That(CountingWriteService<TicketContext, Label, Guid>.Calls, Is.EqualTo(new[] { "Save(Label)" }));
        Assert.That(await tickets.Count(new SearchObject()), Is.EqualTo(1));
        Assert.That(await labels.Count(new SearchObject<Guid>()), Is.EqualTo(1));
    }

    [Test]
    public async Task The_App_Write_Service_Runs_The_Validators()
    {
        await using var sp = Build();
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TicketContext>().Database.EnsureCreatedAsync();

        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket>>();

        await Assert.ThrowsAsync<EntityInputException<Ticket>>(() => tickets.Save(new Ticket()));
        Assert.That(CountingWriteService<TicketContext, Ticket, int>.Calls, Is.EqualTo(new[] { "Save(Ticket)" }));
    }
}
