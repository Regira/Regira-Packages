using System.Data.Common;
using Entities.Providers.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Regira.Entities.DependencyInjection.Reactors;
using Regira.Entities.Reactors;
using Regira.Entities.Reactors.Abstractions;

namespace Entities.Providers.Testing;

/// <summary>
/// Reactors against each provider's own transactions and queries. Npgsql hands the same <c>NpgsqlTransaction</c> to
/// every <c>BeginTransaction</c> on a pooled physical connection, so what waits on one transaction must not outlive
/// it: a transaction disposed without committing leaves nothing for the next one on that connection to run. The
/// stored rows of detached updates are read in one keyed query, which each provider translates in its own way.
/// </summary>
[TestFixtureSource(typeof(ProviderFixtureSource))]
[Category("Containers")]
public class ProviderReactorTests(DbProvider provider)
{
    private readonly List<IEntityChange<Widget>> _reacted = [];
    private ProviderHarness _harness = null!;
    private ServiceProvider _serviceProvider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _harness = new ProviderHarness(provider);
        await _harness.InitializeAsync();

        _serviceProvider = _harness.BuildServiceProvider(services => services.AddReactor<Widget>(_ => new EntityReactor<Widget>((change, _) =>
        {
            lock (_reacted)
            {
                _reacted.Add(change);
            }
            return Task.CompletedTask;
        })));
        using var scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WidgetContext>().Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    [SetUp]
    public void SetUp() => _reacted.Clear();

    [Test]
    public async Task A_Transaction_Disposed_Without_Commit_Does_Not_React_At_The_Next_Commit_On_Its_Connection()
    {
        DbTransaction abandoned, committed;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            abandoned = transaction.GetDbTransaction();
            db.Widgets.Add(new Widget { Title = "Rolled back" });
            await db.SaveChangesAsync();
        }   // disposed without Commit(): the exception path of a request

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            committed = transaction.GetDbTransaction();
            db.Widgets.Add(new Widget { Title = "Committed" });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        TestContext.Out.WriteLine($"{provider} handed out the same DbTransaction again: {ReferenceEquals(abandoned, committed)}");
        Assert.That(_reacted.Select(c => c.Entity.Title), Is.EqualTo(new[] { "Committed" }));
    }

    [Test]
    public async Task Detached_Updates_Report_Their_Stored_Rows()
    {
        int[] ids;
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetContext>();
            var widgets = new[] { new Widget { Title = "Stored one" }, new Widget { Title = "Stored two" } };
            db.Widgets.AddRange(widgets);
            await db.SaveChangesAsync();
            ids = widgets.Select(x => x.Id).ToArray();
        }
        _reacted.Clear();

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetContext>();
            foreach (var id in ids)
            {
                db.Widgets.Update(new Widget { Id = id, Title = $"Renamed {id}", Created = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        Assert.Multiple(() =>
        {
            Assert.That(_reacted.OrderBy(c => c.Entity.Id).Select(c => c.Original!.Title), Is.EqualTo(new[] { "Stored one", "Stored two" }));
            Assert.That(_reacted.All(c => c.HasChanged(x => x.Title) && !c.HasChanged(x => x.IsArchived)), Is.True);
        });
    }
}
