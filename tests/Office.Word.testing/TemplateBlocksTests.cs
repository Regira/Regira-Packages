using System.Text.Json;
using System.Text.Json.Nodes;
using Regira.Office.Word.Models;
using Regira.Office.Word.Templating;

namespace Office.Word.testing;

/// <summary>
/// The syntax and the decision every backend shares; the backends' own fixtures run the same templates through
/// their document models (<c>WordTestsBase</c>, "conditional blocks" and "loop blocks").
/// </summary>
[TestFixture]
public class TemplateBlocksTests
{
    private enum Status { Draft, Sent }

    /// <summary>
    /// The indices of the children <see cref="TemplateBlocks.Resolve"/> does not place, with the conditions decided by
    /// <paramref name="evaluate"/>: every marker, and the content of each branch that does not hold.
    /// </summary>
    private static ISet<int> Removed(IReadOnlyList<string?> children, Func<string, bool> evaluate)
    {
        var placed = TemplateBlocks.Resolve(children, TemplateScope.Root(evaluate)).Select(placement => placement.Child).ToHashSet();
        return Enumerable.Range(0, children.Count).Where(i => !placed.Contains(i)).ToHashSet();
    }

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
        => Assert.That(TemplateBlocks.IsTrue(value), Is.EqualTo(expected));

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
            Assert.That(TemplateBlocks.IsTrue(parameters["Key"]), Is.EqualTo(expected), "JsonElement");
            Assert.That(TemplateBlocks.IsTrue(node), Is.EqualTo(expected), "JsonNode");
        });
    }

    [Test]
    public void A_Json_Value_Built_In_Code_Is_Read_By_Its_Value()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TemplateBlocks.IsTrue(JsonValue.Create(false)), Is.False);
            Assert.That(TemplateBlocks.IsTrue(JsonValue.Create(0)), Is.False);
            Assert.That(TemplateBlocks.IsTrue(JsonValue.Create(" ")), Is.False);
            Assert.That(TemplateBlocks.IsTrue(JsonValue.Create(true)), Is.True);
            Assert.That(TemplateBlocks.IsTrue(JsonValue.Create(3)), Is.True);
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
            Assert.That(TemplateBlocks.Evaluate(input, "ispaid"), Is.True);
            Assert.That(TemplateBlocks.Evaluate(input, "Lines"), Is.True, "a collection with rows");
            Assert.That(TemplateBlocks.Evaluate(input, "NoLines"), Is.False, "a collection without rows");
            Assert.That(TemplateBlocks.Evaluate(input, "Missing"), Is.False);
        });
    }

    [Test]
    public void An_Exact_Key_Comes_Before_One_That_Differs_In_Case()
    {
        var input = new WordTemplateInput
        {
            GlobalParameters = new Dictionary<string, object> { ["items"] = "x" },
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>> { ["Items"] = [] }
        };

        Assert.Multiple(() =>
        {
            Assert.That(TemplateBlocks.Evaluate(input, "Items"), Is.False, "the collection, matched exactly");
            Assert.That(TemplateBlocks.Evaluate(input, "items"), Is.True, "the parameter, matched exactly");
            Assert.That(TemplateBlocks.Evaluate(input, "ITEMS"), Is.True, "no exact match: the parameters come first");
        });
    }

    [Test]
    public void Resolve_Names_The_Markers_And_The_Dropped_Branch()
    {
        string?[] children = ["Intro", "{{#if IsPaid}}", "Paid", null, "{{ else }}", "Due", "{{ /if }}", "Outro"];

        var whenTrue = Removed(children, _ => true);
        var whenFalse = Removed(children, _ => false);

        Assert.Multiple(() =>
        {
            // a non-paragraph child (null) goes with its branch
            Assert.That(whenTrue.Order(), Is.EqualTo(new[] { 1, 4, 5, 6 }));
            Assert.That(whenFalse.Order(), Is.EqualTo(new[] { 1, 2, 3, 4, 6 }));
        });
    }

    [Test]
    public void A_Key_Is_Read_Without_The_Spaces_Around_It()
    {
        string?[] children = ["{{#if IsPaid }}", "Paid", "{{/if}}", "{{#if  ! IsDue}}", "Due", "{{/if}}"];
        var asked = new List<string>();

        var removed = Removed(children, key =>
        {
            asked.Add(key);
            return true;
        });

        Assert.Multiple(() =>
        {
            Assert.That(asked, Is.EqualTo(new[] { "IsPaid", "IsDue" }));
            Assert.That(removed.Order(), Is.EqualTo(new[] { 0, 2, 3, 4, 5 }), "IsPaid holds, !IsDue does not");
        });
    }

    [Test]
    public void A_Negated_Condition_Holds_When_The_Key_Does_Not()
    {
        string?[] children = ["{{#if !IsPaid}}", "Due", "{{/if}}"];

        Assert.That(Removed(children, _ => false).Order(), Is.EqualTo(new[] { 0, 2 }));
    }

    /// <summary>A backend may report a paragraph with its paragraph or cell end character.</summary>
    [Test]
    public void A_Marker_Is_Read_Without_Surrounding_Control_Characters()
    {
        string?[] children = ["{{#if IsPaid}}\r", "Paid\r", "{{/if}}\r\a"];

        Assert.That(Removed(children, _ => false).Order(), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void Only_A_Marker_Makes_A_Paragraph_A_Marker()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TemplateBlocks.ContainsMarker("{{ IsPaid }}"), Is.False, "a parameter");
            Assert.That(TemplateBlocks.ContainsMarker("<{ Appendix }>"), Is.False, "a nested document");
            Assert.That(TemplateBlocks.ContainsMarker("{{ifPaid}}"), Is.False, "a parameter that starts with 'if'");
            Assert.That(TemplateBlocks.ContainsMarker("{{Elsewhere}}"), Is.False, "a parameter that starts with 'else'");
            Assert.That(TemplateBlocks.ContainsMarker("{{#if IsPaid}}"), Is.True);
            Assert.That(TemplateBlocks.ContainsMarker("{{ ELSE }}"), Is.True);
            Assert.That(TemplateBlocks.ContainsMarker("{{else if IsDue}}"), Is.True, "a marker this syntax does not know");
        });
    }

    /// <summary>
    /// A document uses blocks when a paragraph opens one. A closing or alternative marker alone does not count, and
    /// neither does another template language: Go templates write a bare <c>{{else}}</c> too.
    /// </summary>
    [Test]
    public void Only_A_Paragraph_That_Opens_A_Block_Makes_A_Document_Use_Blocks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TemplateBlocks.OpensBlock("{{#if IsPaid}}"), Is.True);
            Assert.That(TemplateBlocks.OpensBlock(" {{ #if !IsPaid }}\r\a"), Is.True);
            Assert.That(TemplateBlocks.OpensBlock("{{/if}}"), Is.False);
            Assert.That(TemplateBlocks.OpensBlock("{{else}}"), Is.False);
            Assert.That(TemplateBlocks.OpensBlock("{{#unless IsPaid}}"), Is.False);
            Assert.That(TemplateBlocks.OpensBlock("{{#each Lines}}"), Is.True);
            Assert.That(TemplateBlocks.OpensBlock("{{/each}}"), Is.False);
            Assert.That(TemplateBlocks.OpensBlock("{{#each}}"), Is.False, "no key");
            Assert.That(TemplateBlocks.OpensBlock("{{#if}}"), Is.False, "no key");
            Assert.That(TemplateBlocks.OpensBlock("Wrap it in {{#if Key}} and {{/if}}."), Is.False);
            Assert.That(TemplateBlocks.OpensBlock("{{#if IsPaid}}Paid{{/if}}"), Is.False);
            Assert.That(TemplateBlocks.OpensBlock("{{ IsPaid }}"), Is.False);
            Assert.That(TemplateBlocks.OpensBlock(null), Is.False);
        });
    }

    [Test]
    public void A_Block_Without_A_Key_Fails()
    {
        var ex = Assert.Throws<FormatException>(() => Removed(["{{#if}}", "{{/if}}"], _ => true));

        Assert.That(ex!.Message, Does.Contain("{{#if Key}}"));
    }

    [TestCase("{{#unless IsPaid}}")]
    [TestCase("{{else if IsDue}}")]
    [TestCase("{{/unless}}")]
    [TestCase("{{#each}}")]
    [TestCase("{{#each !Lines}}")]
    public void A_Marker_This_Syntax_Does_Not_Know_Fails(string marker)
    {
        var ex = Assert.Throws<FormatException>(() => Removed(["{{#if IsPaid}}", marker, "{{/if}}"], _ => true));

        Assert.That(ex!.Message, Does.Contain(marker).And.Contain("not a template marker"));
    }

    /// <summary>
    /// The document decides how long a paragraph is: a marker's runs of white space must not make reading one
    /// quadratic, which took seconds at twenty thousand spaces.
    /// </summary>
    [Test]
    public void A_Long_Run_Of_Spaces_Is_Read_In_Linear_Time()
    {
        var text = "{{#if" + new string(' ', 200_000) + "}}";
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var opens = TemplateBlocks.OpensBlock(text);
        var ex = Assert.Throws<FormatException>(() => Removed(["{{#if IsPaid}}", text, "{{/if}}"], _ => true));

        Assert.Multiple(() =>
        {
            Assert.That(opens, Is.False, "no key");
            Assert.That(ex!.Message, Does.Contain("not a template marker"));
            Assert.That(ex.Message, Has.Length.LessThan(300), "the marker is quoted cut short");
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        });
    }

    [Test]
    public void A_Paragraph_Quoted_In_A_Message_Is_Cut_Short()
    {
        var paragraph = "Dear {{#if IsCompany}}Sir or Madam{{/if}}, " + new string('x', 500);

        var ex = Assert.Throws<FormatException>(() => Removed(["{{#if IsPaid}}", paragraph, "{{/if}}"], _ => true));

        Assert.That(ex!.Message, Does.Contain("Dear {{#if IsCompany}}").And.Not.Contain(new string('x', 100)));
    }

    [Test]
    public void Deeply_Nested_Blocks_Drop_What_An_Outer_Block_Drops()
    {
        // twenty levels, the outermost false: every child between its markers goes, whatever the inner blocks say
        var opening = Enumerable.Range(0, 20).Select(i => $"{{{{#if Level{i}}}}}");
        var closing = Enumerable.Repeat("{{/if}}", 20);
        string?[] children = ["Intro", .. opening, "Deep", .. closing, "Outro"];

        var removed = Removed(children, key => key != "Level0");

        Assert.That(Enumerable.Range(0, children.Length).Where(i => !removed.Contains(i)).Select(i => children[i]),
            Is.EqualTo(new[] { "Intro", "Outro" }));
    }

    // ---- loops ----

    private static WordTemplateInput Orders(object? lines = null) => new()
    {
        GlobalParameters = new Dictionary<string, object> { ["Currency"] = "EUR", ["Region"] = "global" },
        CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
        {
            ["Orders"] =
            [
                new Dictionary<string, object> { ["Number"] = "A-1", ["Region"] = "north", ["IsUrgent"] = true, ["Lines"] = lines ?? new[] { new { Product = "Pen" }, new { Product = "Ink" } } },
                new Dictionary<string, object> { ["Number"] = "A-2", ["IsUrgent"] = false, ["Lines"] = Array.Empty<object>() }
            ],
            ["None"] = []
        }
    };

    private static string[] Written(IReadOnlyList<string?> children, IReadOnlyList<Placement> placements)
        => placements.Select(placement => $"{children[placement.Child]}@{placement.Scope.RowNumber}").ToArray();

    [Test]
    public void A_Loop_Places_Its_Content_Once_Per_Row()
    {
        string?[] children = ["Intro", "{{#each Orders}}", "Order", null, "{{/each}}", "Outro"];

        var placements = TemplateBlocks.Resolve(children, TemplateScope.Root(Orders()));

        Assert.Multiple(() =>
        {
            // a non-paragraph child (null) repeats with the paragraphs around it
            Assert.That(placements.Select(placement => placement.Child), Is.EqualTo(new[] { 0, 2, 3, 2, 3, 5 }));
            Assert.That(placements.Select(placement => placement.Scope.RowNumber), Is.EqualTo(new[] { 0, 1, 1, 2, 2, 0 }));
            Assert.That(placements[0].Scope.IsRoot && placements[^1].Scope.IsRoot, Is.True);
        });
    }

    [Test]
    public void A_Loop_Without_Rows_Writes_Its_Else()
    {
        string?[] missing = ["{{#each Missing}}", "Row", "{{else}}", "No rows", "{{/each}}"];
        string?[] empty = ["{{#each None}}", "Row", "{{ ELSE }}", "No rows", "{{/EACH}}"];
        string?[] withoutElse = ["{{#each None}}", "Row", "{{/each}}", "Outro"];
        var scope = TemplateScope.Root(Orders());

        Assert.Multiple(() =>
        {
            Assert.That(Written(missing, TemplateBlocks.Resolve(missing, scope)), Is.EqualTo(new[] { "No rows@0" }));
            Assert.That(Written(empty, TemplateBlocks.Resolve(empty, scope)), Is.EqualTo(new[] { "No rows@0" }));
            Assert.That(Written(withoutElse, TemplateBlocks.Resolve(withoutElse, scope)), Is.EqualTo(new[] { "Outro@0" }));
        });
    }

    [Test]
    public void A_Loop_Runs_Over_A_Collection_Its_Row_Holds()
    {
        string?[] children = ["{{#each Orders}}", "{{Number}}", "{{#each Lines}}", "{{Product}}", "{{else}}", "No lines", "{{/each}}", "{{/each}}"];

        var placements = TemplateBlocks.Resolve(children, TemplateScope.Root(Orders()));
        var lines = placements.Where(placement => children[placement.Child] == "{{Product}}").Select(placement => placement.Scope).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Written(children, placements), Is.EqualTo(new[] { "{{Number}}@1", "{{Product}}@1", "{{Product}}@2", "{{Number}}@2", "No lines@2" }));
            Assert.That(lines.Select(line => line.Fields("{{Product}} for {{ number }}").Select(field => field.Value)),
                Is.EqualTo(new[] { new[] { "Pen", "A-1" }, new[] { "Ink", "A-1" } }), "the row, then the row around it");
        });
    }

    [Test]
    public void A_Field_Reads_The_Row_Then_The_Rows_Around_It_And_Leaves_The_Rest_To_The_Global_Pass()
    {
        var order = TemplateScope.Root(Orders()).Rows("Orders")[0];
        var line = order.Rows("Lines")[1];

        Assert.Multiple(() =>
        {
            Assert.That(line.TryGetField("Product", out var product) ? product : null, Is.EqualTo("Ink"));
            Assert.That(line.TryGetField("Number", out var number) ? number : null, Is.EqualTo("A-1"));
            Assert.That(line.TryGetField("row_number", out var position) ? position : null, Is.EqualTo(2), "the innermost row's");
            Assert.That(line.TryGetField("Currency", out _), Is.False, "a global: the global pass fills it");
            Assert.That(line.Fields("{{Region}} {{ Currency }} {{Missing}}").Select(field => (field.Key, field.Value)),
                Is.EqualTo(new[] { ("Region", "north") }), "a row's field before the global of the same name");
            Assert.That(TemplateScope.Root(Orders()).Fields("{{Number}}"), Is.Empty, "nothing is a row's at the root");
        });
    }

    [Test]
    public void A_Field_Matches_Its_Key_Regardless_Of_Case_An_Exact_Match_First()
    {
        var input = new WordTemplateInput
        {
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
            {
                ["Rows"] = [new Dictionary<string, object> { ["name"] = "lower", ["Name"] = "exact", ["Title"] = "title" }]
            }
        };
        var row = TemplateScope.Root(input).Rows("ROWS").Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.TryGetField("Name", out var exact) ? exact : null, Is.EqualTo("exact"));
            Assert.That(row.TryGetField("TITLE", out var title) ? title : null, Is.EqualTo("title"));
        });
    }

    [Test]
    public void A_Condition_In_A_Loop_Reads_The_Row_Then_The_Input()
    {
        string?[] children = ["{{#each Orders}}", "{{#if IsUrgent}}", "Urgent", "{{/if}}", "{{#if Currency}}", "Priced", "{{/if}}", "{{#if Lines}}", "Has lines", "{{/if}}", "{{/each}}"];

        var placements = TemplateBlocks.Resolve(children, TemplateScope.Root(Orders()));

        Assert.That(Written(children, placements), Is.EqualTo(new[] { "Urgent@1", "Priced@1", "Has lines@1", "Priced@2" }));
    }

    [Test]
    public void Rows_Are_Read_From_Json_Objects_And_Arrays()
    {
        const string json = """
            { "Orders": [ { "Number": "J-1", "Lines": [ { "Product": "Pen", "Qty": 2 }, { "Product": "Ink", "Qty": null } ] } ] }
            """;
        var collections = JsonSerializer.Deserialize<Dictionary<string, ICollection<IDictionary<string, object>>>>(json)!;
        var element = TemplateScope.Root(new WordTemplateInput { CollectionParameters = collections }).Rows("Orders").Single();
        var node = TemplateScope.Root(Orders(JsonNode.Parse("""[ { "Product": "Paper" } ]"""))).Rows("Orders")[0];

        Assert.Multiple(() =>
        {
            Assert.That(element.Rows("Lines").Select(line => string.Join("|", line.Fields("{{Product}} {{Qty}}").Select(field => field.Value))),
                Is.EqualTo(new[] { "Pen|2", "Ink|" }), "a JsonElement array, a string and a number by kind, null as nothing");
            Assert.That(node.Rows("Lines").Single().Fields("{{Product}}").Single().Value, Is.EqualTo("Paper"), "a JsonArray");
        });
    }

    [Test]
    public void A_Key_That_Names_No_List_Of_Rows_Gives_None()
    {
        var root = TemplateScope.Root(Orders());
        var order = root.Rows("Orders")[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.Rows("Currency"), Is.Empty, "a global parameter");
            Assert.That(root.Rows("Missing"), Is.Empty);
            Assert.That(order.Rows("Number"), Is.Empty, "a row's text field");
            Assert.That(root.Rows("orders"), Has.Count.EqualTo(2), "a collection, regardless of case");
        });
    }

    [Test]
    public void A_Closer_Of_The_Other_Kind_Fails()
    {
        var scope = TemplateScope.Root(Orders());

        var ifClosesLoop = Assert.Throws<FormatException>(() => TemplateBlocks.Resolve(["{{#each Orders}}", "Row", "{{/if}}"], scope));
        var loopClosesIf = Assert.Throws<FormatException>(() => TemplateBlocks.Resolve(["{{#if IsPaid}}", "Paid", "{{/each}}"], scope));
        var unclosed = Assert.Throws<FormatException>(() => TemplateBlocks.Resolve(["{{#each Orders}}", "Row"], scope));
        var unopened = Assert.Throws<FormatException>(() => TemplateBlocks.Resolve(["Row", "{{/each}}"], scope));

        Assert.Multiple(() =>
        {
            Assert.That(ifClosesLoop!.Message, Does.Contain("{{/if}}").And.Contain("{{#each Orders}}").And.Contain("{{/each}}"));
            Assert.That(loopClosesIf!.Message, Does.Contain("{{/each}}").And.Contain("{{#if IsPaid}}").And.Contain("{{/if}}"));
            Assert.That(unclosed!.Message, Does.Contain("{{#each Orders}}").And.Contain("{{/each}}"));
            Assert.That(unopened!.Message, Does.Contain("{{/each}}").And.Contain("{{#each}}"));
        });
    }

    [Test]
    public void A_Loop_Holding_A_Note_Or_Comment_Fails_Whatever_Its_Rows()
    {
        string?[] children = ["{{#each None}}", "Footnoted", "{{else}}", "Also footnoted", "{{/each}}", "Footnoted outside"];
        bool HoldsNote(int i) => children[i]!.Contains("ootnoted");

        var ex = Assert.Throws<FormatException>(() => TemplateBlocks.Resolve(children, TemplateScope.Root(Orders()), HoldsNote));
        var inElse = TemplateBlocks.Resolve(["{{#each None}}", "Row", "{{else}}", "Footnoted", "{{/each}}"], TemplateScope.Root(Orders()), i => i == 3);

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("{{#each None}}").And.Contain("footnote"));
            Assert.That(inElse.Select(placement => placement.Child), Is.EqualTo(new[] { 3 }), "a loop's else is not copied");
        });
    }

    [Test]
    public void Fields_Skip_Markers_And_Tags_No_Row_Holds()
    {
        var row = TemplateScope.Root(Orders()).Rows("Orders")[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Fields("{{#if Number}}"), Is.Empty);
            Assert.That(row.Fields("<{ Number }>"), Is.Empty, "a nested document");
            Assert.That(row.Fields("{{ Number }} and {{NUMBER}}").Select(field => field.Key), Is.EqualTo(new[] { "Number" }));
            Assert.That(row.Fields("{{ Number }}").Single().Pattern.Replace("No. {{  number }}.", "A-1"), Is.EqualTo("No. A-1."));
        });
    }

    private sealed class Order
    {
        public string Number { get; init; } = "";
        public List<OrderLine> Lines { get; } = [];
    }

    private sealed class OrderLine
    {
        public string Product { get; init; } = "";
        public Uri? Link { get; init; }
        public required Order Order { get; init; }
    }

    /// <summary>
    /// An object row is read one level deep, as its fields ask for it: a reference back to the row around it, as an
    /// ORM's navigation property holds one, is a value like any other, and a value that is no list is written as itself.
    /// </summary>
    [Test]
    public void An_Object_Row_Is_Read_Without_Following_Its_References()
    {
        var order = new Order { Number = "A-1" };
        order.Lines.Add(new OrderLine { Product = "Pen", Link = new Uri("https://example.com/pen"), Order = order });
        var input = new WordTemplateInput
        {
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
            {
                ["Orders"] = [new Dictionary<string, object> { ["Number"] = order.Number, ["Lines"] = order.Lines }]
            }
        };

        var line = TemplateScope.Root(input).Rows("Orders").Single().Rows("Lines").Single();

        Assert.Multiple(() =>
        {
            Assert.That(line.Fields("{{Product}} {{Link}}").Select(field => field.Value), Is.EqualTo(new[] { "Pen", "https://example.com/pen" }));
            Assert.That(line.Rows("Order"), Is.Empty, "a single object is no list of rows");
        });
    }
}
