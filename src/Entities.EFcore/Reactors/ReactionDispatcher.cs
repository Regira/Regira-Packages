using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.Reactors.Abstractions;
using System.Diagnostics;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Runs the reactors for the changes of a committed save: in registration order, per change, in a DI scope of their
/// own. Each reactor is isolated — the data is already committed, so a failure is logged instead of thrown, and the
/// reactors after it start from a fresh scope rather than inherit the half-staged changes it may have left tracked.
/// </summary>
internal sealed class ReactionDispatcher(IServiceProvider serviceProvider)
{
    // Resolved up front: a reaction deferred to an ambient transaction's completion can run after the scope that saved
    // is disposed. The scope factory a scope hands out creates its scopes from the root provider.
    private readonly IServiceScopeFactory _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    private readonly IServiceCollection? _services = serviceProvider.GetService<IServiceCollection>();
    private readonly ILogger? _logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<EntityReactorInterceptor>();

    /// <summary>
    /// How deeply reactions may nest: a reactor's own save triggers the reactors of what it wrote, and a chain that
    /// keeps writing what triggers it would otherwise never end.
    /// </summary>
    internal const int MaxDepth = 8;

    private static readonly AsyncLocal<int> Depth = new();

    public async Task Dispatch(IReadOnlyList<IEntityChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }
        if (Depth.Value >= MaxDepth)
        {
            Report(null, "Reactions are nested {MaxDepth} levels deep: the {Count} committed change(s) of this save are not reacted to. " +
                         "A reactor keeps writing rows that trigger reactors again — guard its CanReact so the chain ends.", MaxDepth, changes.Count);
            return;
        }

        Depth.Value++;
        var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            var reactors = Resolve(scope.ServiceProvider);
            foreach (var change in changes)
            {
                var entityType = change.Entity.GetType();
                for (var i = 0; i < reactors.Length; i++)
                {
                    if (!reactors[i].Handles(entityType))
                    {
                        continue;
                    }
                    var reactor = reactors[i].Reactor;
                    try
                    {
                        if (reactor.CanReact(change))
                        {
                            // not the save's token: the data is committed, and a client that goes away must not cancel what follows from it
                            await reactor.React(change, CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        Report(ex, "Reactor {Reactor} failed for a committed {Kind} of {EntityType} #{Id} — the save stands; the other reactors still run",
                            reactor.GetType().FullName, change.Kind, entityType.FullName, IdOf(change.Entity));
                        await scope.DisposeAsync();
                        scope = _scopeFactory.CreateAsyncScope();
                        reactors = Resolve(scope.ServiceProvider);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // resolving the reactors themselves failed (a misconfigured registration) — still not the save's failure
            Report(ex, "Could not run the reactors for {Count} committed change(s)", changes.Count);
        }
        finally
        {
            await scope.DisposeAsync();
            Depth.Value--;
        }
    }

    private RegisteredReactor[] Resolve(IServiceProvider scopedProvider)
        => _services != null
            ? ReactorDiscovery.GetReactors(scopedProvider, _services)
            : ReactorDiscovery.Unregistered(scopedProvider.GetServices<IEntityReactor>());

    // a reactor failure must never vanish: without a logger (a bare ServiceCollection) it goes to the trace listeners
    internal void Report(Exception? ex, string message, params object?[] args)
    {
        if (_logger != null)
        {
            _logger.LogError(ex, message, args);
            return;
        }
        Trace.TraceError($"{message} [{string.Join(", ", args)}] {ex}");
    }

    private static object? IdOf(object entity)
        => entity.GetType().GetProperty("Id")?.GetValue(entity);
}
