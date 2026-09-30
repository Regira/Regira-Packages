using System.Text.Json;
using System.Text.Json.Nodes;
using Regira.Office.Word.Models;
using Regira.Office.Word.Templating;

namespace Office.Word.testing;

/// <summary>
/// The syntax and the decision every backend shares; the backends' own fixtures run the same templates through
/// their document models (<c>WordTestsBase</c>, "conditional blocks").
/// </summary>
[TestFixture]
public class ConditionalBlocksTests
{
    private enum Status { Draft, Sent }

    private static IEnumerable<TestCaseData> Values()
    {
        yield return new TestCaseData(null, false).SetArgDisplayNames("null");
        yield return new TestCaseData(false, false).SetArgDisplayNames("false");
        yield return new TestCaseData(true, true).SetArgDisplayNames("true");
        yield return new TestCaseData("", false).SetArgDisplayNames("empty string");
        yield return new TestCaseData("  ", false).SetArgDisplayNames("blank string");
        yield return new TestCaseData("false", true).SetArgDisplayNames("the text 'false'");
        yield return new TestCaseData(0, false).SetArgDisplayNames("0");
        yield return new TestCaseData(0m, false).SetArgDisplayNames("0m");
        yield return new TestCaseData(double.NaN, false).SetArgDisplayNames("NaN");
        yield return new TestCaseData(-1, true).SetArgDisplayNames("-1");
        yield return new TestCaseData(0.5, true).SetArgDisplayNames("0.5");
        yield return new TestCaseData(DBNull.Value, false).SetArgDisplayNames("DBNull");
        yield return new TestCaseData(Status.Draft, true).SetArgDisplayNames("an enum's zero member");
        yield return new TestCaseData(new DateTime(2026, 9, 27), true).SetArgDisplayNames("a date");
        yield return new TestCaseData(Array.Empty<string>(), false).SetArgDisplayNames("empty array");
        yield return new TestCaseData(new List<int> { 1 }, true).SetArgDisplayNames("list with an item");
        yield return new TestCaseData(Enumerable.Empty<int>().Select(x => x), false).SetArgDisplayNames("empty sequence");
        yield return new TestCaseData(new object(), true).SetArgDisplayNames("object");
    }

    [TestCaseSource(nameof(Values))]
    public void A_Value_Is_True_Unless_Empty(object? value, bool expected)
        => Assert.That(ConditionalBlocks.IsTrue(value), Is.EqualTo(expected));

    /// <summary>
    /// Parameters sent to the Office API arrive as JSON values once System.Text.Json deserialises them.
    /// </summary>
    [TestCase("true", true)]
    [TestCase("false", false)]
    [TestCase("null", false)]
    [TestCase("\"\"", false)]
    [TestCase("\"yes\"", true)]
    [TestCase("0", false)]
    [TestCase("0.0", false)]
    [TestCase("3", true)]
    [TestCase("[]", false)]
    [TestCase("[1]", true)]
    [TestCase("{}", false)]
    [TestCase("{\"a\": 1}", true)]
    public void A_Json_Value_Is_Read_By_Its_Kind(string json, bool expected)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>($"{{\"Key\": {json}}}")!;
        var node = JsonNode.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(ConditionalBlocks.IsTrue(parameters["Key"]), Is.EqualTo(expected), "JsonElement");
            Assert.That(ConditionalBlocks.IsTrue(node), Is.EqualTo(expected), "JsonNode");
        });
    }

    [Test]
    public void A_Json_Value_Built_In_Code_Is_Read_By_Its_Value()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConditionalBlocks.IsTrue(JsonValue.Create(false)), Is.False);
            Assert.That(ConditionalBlocks.IsTrue(JsonValue.Create(0)), Is.False);
            Assert.That(ConditionalBlocks.IsTrue(JsonValue.Create(" ")), Is.False);
            Assert.That(ConditionalBlocks.IsTrue(JsonValue.Create(true)), Is.True);
            Assert.That(ConditionalBlocks.IsTrue(JsonValue.Create(3)), Is.True);
        });
    }

    [Test]
    public void A_Key_Is_Found_In_The_Parameters_Regardless_Of_Case()
    {
        var input = new WordTemplateInput
        {
            GlobalParameters = new Dictionary<string, object> { ["IsPaid"] = true },
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
            {
                ["Lines"] = [new Dictionary<string, object> { ["Title"] = "One" }],
                ["NoLines"] = []
            }
        };

        Assert.Multiple(() =>
        {
            Assert.That(ConditionalBlocks.Evaluate(input, "ispaid"), Is.True);
            Assert.That(ConditionalBlocks.Evaluate(input, "Lines"), Is.True, "a collection with rows");
            Assert.That(ConditionalBlocks.Evaluate(input, "NoLines"), Is.False, "a collection without rows");
            Assert.That(ConditionalBlocks.Evaluate(input, "Missing"), Is.False);
        });
    }

    [Test]
    public void Resolve_Names_The_Markers_And_The_Dropped_Branch()
    {
        string?[] children = ["Intro", "{{#if IsPaid}}", "Paid", null, "{{ else }}", "Due", "{{ /if }}", "Outro"];

        var whenTrue = ConditionalBlocks.Resolve(children, _ => true);
        var whenFalse = ConditionalBlocks.Resolve(children, _ => false);

        Assert.Multiple(() =>
        {
            // a non-paragraph child (null) goes with its branch
            Assert.That(whenTrue.Order(), Is.EqualTo(new[] { 1, 4, 5, 6 }));
            Assert.That(whenFalse.Order(), Is.EqualTo(new[] { 1, 2, 3, 4, 6 }));
        });
    }

    [Test]
    public void A_Negated_Condition_Holds_When_The_Key_Does_Not()
    {
        string?[] children = ["{{#if !IsPaid}}", "Due", "{{/if}}"];

        Assert.That(ConditionalBlocks.Resolve(children, _ => false).Order(), Is.EqualTo(new[] { 0, 2 }));
    }

    /// <summary>A backend may report a paragraph with its paragraph or cell end character.</summary>
    [Test]
    public void A_Marker_Is_Read_Without_Surrounding_Control_Characters()
    {
        string?[] children = ["{{#if IsPaid}}\r", "Paid\r", "{{/if}}\r\a"];

        Assert.That(ConditionalBlocks.Resolve(children, _ => false).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void Only_A_Marker_Makes_A_Paragraph_A_Marker()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConditionalBlocks.ContainsMarker("{{ IsPaid }}"), Is.False, "a parameter");
            Assert.That(ConditionalBlocks.ContainsMarker("<{ Appendix }>"), Is.False, "a nested document");
            Assert.That(ConditionalBlocks.ContainsMarker("{{ifPaid}}"), Is.False, "a parameter that starts with 'if'");
            Assert.That(ConditionalBlocks.ContainsMarker("{{Elsewhere}}"), Is.False, "a parameter that starts with 'else'");
            Assert.That(ConditionalBlocks.ContainsMarker("{{#if IsPaid}}"), Is.True);
            Assert.That(ConditionalBlocks.ContainsMarker("{{ ELSE }}"), Is.True);
            Assert.That(ConditionalBlocks.ContainsMarker("{{else if IsDue}}"), Is.True, "a marker this syntax does not know");
        });
    }

    /// <summary>
    /// A document uses blocks when a paragraph opens one. A closing or alternative marker alone does not count, and
    /// neither does another template language: Handlebars and Go templates write a bare <c>{{else}}</c> too.
    /// </summary>
    [Test]
    public void Only_A_Paragraph_That_Opens_A_Block_Makes_A_Document_Use_Blocks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConditionalBlocks.OpensBlock("{{#if IsPaid}}"), Is.True);
            Assert.That(ConditionalBlocks.OpensBlock(" {{ #if !IsPaid }}\r\a"), Is.True);
            Assert.That(ConditionalBlocks.OpensBlock("{{/if}}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{else}}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{#unless IsPaid}}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{#each items}}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{#if}}"), Is.False, "no key");
            Assert.That(ConditionalBlocks.OpensBlock("Wrap it in {{#if Key}} and {{/if}}."), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{#if IsPaid}}Paid{{/if}}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{ IsPaid }}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock(null), Is.False);
        });
    }

    [Test]
    public void A_Block_Without_A_Key_Fails()
    {
        var ex = Assert.Throws<FormatException>(() => ConditionalBlocks.Resolve(["{{#if}}", "{{/if}}"], _ => true));

        Assert.That(ex!.Message, Does.Contain("{{#if Key}}"));
    }

    [TestCase("{{#unless IsPaid}}")]
    [TestCase("{{else if IsDue}}")]
    [TestCase("{{/unless}}")]
    [TestCase("{{#each Lines}}")]
    public void A_Marker_This_Syntax_Does_Not_Know_Fails(string marker)
    {
        var ex = Assert.Throws<FormatException>(() => ConditionalBlocks.Resolve(["{{#if IsPaid}}", marker, "{{/if}}"], _ => true));

        Assert.That(ex!.Message, Does.Contain(marker).And.Contain("not a conditional marker"));
    }
}
