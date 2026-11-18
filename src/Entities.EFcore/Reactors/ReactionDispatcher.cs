using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.Reactors.Abstractions;
using System.Diagnostics;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Runs the reactors for the changes of a commit, in a DI scope of their own: reactor by reactor in registration
/// order, each with the changes it covers in the order they were saved — one call for an
/// <see cref="IEntityBatchReactor"/>, row by row for any other. A reactor is instantiated in that scope only when a
/// change of a type it reacts to is among them. Each reaction is isolated — the data is already committed, so a failure
/// is logged instead of thrown, and what runs after it starts from a fresh scope rather than inherit the half-staged
/// changes it may have left tracked.
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

    /// <summary>
    /// Whether <paramref name="other"/> creates its scopes from the same provider, and so runs the same reactors: the
    /// saves of contexts sharing a transaction are then one batch.
    /// </summary>
    public bool SharesScopesWith(ReactionDispatcher other) => ReferenceEquals(_scopeFactory, other._scopeFactory);

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

        AsyncServiceScope scope;
        try
        {
            scope = _scopeFactory.CreateAsyncScope();
        }
        catch (Exception ex)
        {
            // the provider is gone (disposed while a transaction was still open): nothing to run the reactors in
            Report(ex, "Could not run the reactors for {Count} committed change(s)", changes.Count);
            return;
        }

        Depth.Value++;
        try
        {
            var reactors = InScope(scope.ServiceProvider);
            // what runs after a failure starts clean
            async Task Renew()
            {
                await Dispose(scope);
                scope = _scopeFactory.CreateAsyncScope();
                reactors = InScope(scope.ServiceProvider);
            }

            for (var i = 0; i < reactors.Count; i++)
            {
                var covered = changes.Where(change => reactors.Handles(i, change.Entity.GetType())).ToArray();
                if (covered.Length == 0)
                {
                    continue;
                }

                IEntityReactor? reactor;
                try
                {
                    reactor = reactors.Get(i);
                }
                catch (Exception ex)
                {
                    Report(ex, "Reactor {Reactor} could not be created for {Count} committed change(s) — the save stands; the other reactors still run",
                        reactors.NameOf(i), covered.Length);
                    await Renew();
                    continue;
                }

                // not the save's token: the data is committed, and a client that goes away must not cancel what follows from it
                if (reactor is IEntityBatchReactor batchReactor)
                {
                    var selected = covered.Length;
                    try
                    {
                        var changesOfBatch = covered.Where(reactor.CanReact).ToArray();
                        selected = changesOfBatch.Length;
                        if (selected > 0)
                        {
                            await batchReactor.ReactMany(changesOfBatch, CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        Report(ex, "Reactor {Reactor} failed for a committed batch of {Count} change(s) — the save stands; the other reactors still run",
                            reactors.NameOf(i), selected);
                        await Renew();
                    }
                    continue;
                }

                foreach (var change in covered)
                {
                    try
                    {
                        // the reactor of the current scope: a fresh one after a row that failed
                        if (reactors.Get(i) is { } rowReactor && rowReactor.CanReact(change))
                        {
                            await rowReactor.React(change, CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        Report(ex, "Reactor {Reactor} failed for a committed {Kind} of {EntityType} #{Id} — the save stands; the other reactors still run",
                            reactors.NameOf(i), change.Kind, change.Entity.GetType().FullName, IdOf(change.Entity));
                        await Renew();
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
            await Dispose(scope);
            Depth.Value--;
        }
    }

    // A scoped service that fails to dispose — a buffer flushing what the reactors handed it — is one more failure
    // after the commit: logged, never the save's.
    private async Task Dispose(AsyncServiceScope scope)
    {
        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception ex)
        {
            Report(ex, "A service of the reactors' scope failed when the scope was disposed — the save stands");
        }
    }

    private ScopedReactors InScope(IServiceProvider scopedProvider)
        => new(_services != null ? ReactorDiscovery.GetCatalog(_services).Registrations : [], scopedProvider);

    /// <summary>The reactors of one DI scope, each instantiated the first time a change needs it.</summary>
    private sealed class ScopedReactors(ReactorRegistration[] registrations, IServiceProvider scopedProvider)
    {
        private readonly IEntityReactor?[] _reactors = new IEntityReactor?[registrations.Length];
        private readonly bool[] _materialized = new bool[registrations.Length];

        public int Count => registrations.Length;
        public bool Handles(int index, Type entityType) => registrations[index].Handles(entityType);
        public string NameOf(int index) => _reactors[index]?.GetType().FullName ?? registrations[index].Name;

        public IEntityReactor? Get(int index)
        {
            if (!_materialized[index])
            {
                _materialized[index] = true;
                _reactors[index] = registrations[index].Materialize(scopedProvider);
            }
            return _reactors[index];
        }
    }

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
