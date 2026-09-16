namespace Regira.Office.Word.Syncfusion.Internal;

/// <summary>
/// Bounds how deeply document builds nest — nested document parameters, headers and footers each build a document
/// inside the one being built — so a template that includes itself fails instead of recursing without end.
/// <para>
/// The depth belongs to the current flow of execution rather than to a service instance: a long-lived instance
/// never runs out, and concurrent calls never share a count. Builds run depth-first, so a cycle reaches the limit
/// on its first path, however many placeholders each level holds.
/// </para>
/// </summary>
internal static class NestedDocumentGuard
{
    /// <summary>The number of builds nested inside the outermost one.</summary>
    public const int MaxDepth = 100;

    private static readonly AsyncLocal<int> Depth = new();

    /// <summary>
    /// Opens one document build; dispose the result when the build is done.
    /// </summary>
    /// <exception cref="InvalidOperationException">The build would nest deeper than <see cref="MaxDepth"/></exception>
    public static Scope Enter()
    {
        if (Depth.Value > MaxDepth)
        {
            throw new InvalidOperationException(
                $"Maximum insertable documents reached: documents nest more than {MaxDepth} levels deep. " +
                "A template that includes itself — as a nested document, a header or a footer — never ends.");
        }

        Depth.Value++;
        return new Scope();
    }

    public readonly struct Scope : IDisposable
    {
        public void Dispose() => Depth.Value--;
    }
}
