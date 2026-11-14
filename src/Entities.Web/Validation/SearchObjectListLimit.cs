using Regira.Entities.Models;

namespace Regira.Entities.Web.Validation;

/// <summary>
/// The most search objects one <c>POST list</c> or <c>POST search</c> body may carry. Each one adds a <c>UNION</c> to
/// the query, so an unbounded array grows the expression tree until translating it overflows the stack.
/// </summary>
internal static class SearchObjectListLimit
{
    public const int Max = 32;

    /// <summary>Refuses a list longer than <see cref="Max"/> with the 400 of a refused input.</summary>
    public static void ThrowIfTooLong<TEntity, TSearchObject>(IReadOnlyCollection<TSearchObject>? searchObjects)
    {
        if (searchObjects?.Count > Max)
        {
            throw new EntityInputException<TEntity>("The request body is not valid.")
            {
                Errors = [new EntityInputError(string.Empty, $"A request takes at most {Max} search objects.", new Dictionary<string, object?> { ["max"] = Max })]
            };
        }
    }
}
