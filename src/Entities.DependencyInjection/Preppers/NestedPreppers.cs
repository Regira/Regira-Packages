using Regira.Entities.Preppers;
using Regira.Entities.Preppers.Abstractions;

namespace Regira.Entities.DependencyInjection.Preppers;

internal static class NestedPreppers
{
    /// <summary>
    /// Prefixes <paramref name="nested"/> with the <c>[ServerOwned]</c> restore: an owned child is written
    /// through its parent's prepper chain, never its own write service, so the globally registered
    /// <see cref="AutoServerOwnedPrepper"/> would otherwise never see it.
    /// </summary>
    public static IEnumerable<IEntityPrepper<TRelated>> WithServerOwned<TRelated>(IEnumerable<IEntityPrepper<TRelated>>? nested = null)
        where TRelated : class
        => nested == null
            ? [new AutoServerOwnedPrepper<TRelated>()]
            : nested.Prepend(new AutoServerOwnedPrepper<TRelated>());
}
