using Regira.Office.Word.Templating;

namespace Office.Word.testing;

/// <summary>
/// <c>NestedDocumentGuard</c> is what the Spire, Syncfusion and Aspose backends enter on every document build. Its depth
/// belongs to the flow of execution: a long-lived service never wears it out, and concurrent builds never share a count.
/// Each backend's fixture checks that a build of 101 nested documents leaves the depth as it found it
/// (<c>Nested_Documents_Do_Not_Wear_Out_The_Service</c>) and that a template including itself fails.
/// </summary>
[TestFixture]
public class NestedDocumentGuardTests
{
    /// <summary>The outermost build and the 100 nested ones the guard allows.</summary>
    internal const int FullDepth = 101;

    /// <summary>Opens nested builds in this flow until the guard refuses one, closes them, and returns how many it opened.</summary>
    internal static int OpenUntilRefused()
    {
        var scopes = new Stack<NestedDocumentGuard.Scope>();
        try
        {
            while (scopes.Count <= FullDepth)
            {
                scopes.Push(NestedDocumentGuard.Enter());
            }
            return scopes.Count;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Maximum insertable documents"))
        {
            return scopes.Count;
        }
        finally
        {
            while (scopes.Count > 0)
            {
                scopes.Pop().Dispose();
            }
        }
    }

    [Test]
    public void Allows_The_Outermost_Build_And_100_Nested_Ones()
    {
        Assert.That(OpenUntilRefused(), Is.EqualTo(FullDepth));
    }

    [Test]
    public void Sequential_Builds_Never_Wear_It_Out()
    {
        for (var i = 0; i < 200; i++)
        {
            using var build = NestedDocumentGuard.Enter();
        }

        Assert.That(OpenUntilRefused(), Is.EqualTo(FullDepth));
    }

    [Test]
    public async Task Concurrent_Builds_Keep_Their_Own_Count()
    {
        using var bothHalfway = new Barrier(2);
        int Build()
        {
            var scopes = new Stack<NestedDocumentGuard.Scope>();
            try
            {
                for (var i = 0; i < 50; i++)
                {
                    scopes.Push(NestedDocumentGuard.Enter());
                }
                // both flows hold 50 levels before either goes deeper
                Assert.That(bothHalfway.SignalAndWait(TimeSpan.FromSeconds(10)), Is.True, "the other flow reached 50 levels");
                return scopes.Count + OpenUntilRefused();
            }
            finally
            {
                while (scopes.Count > 0)
                {
                    scopes.Pop().Dispose();
                }
            }
        }

        var depths = await Task.WhenAll(Task.Run(Build), Task.Run(Build));

        Assert.That(depths, Is.EqualTo(new[] { FullDepth, FullDepth }));
    }
}
