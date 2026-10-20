using Regira.Office.Word.Models;
using Regira.Office.Word.Templating;

namespace Office.Word.testing;

/// <summary>
/// <c>ConditionalBlocks</c> is what the Word backends published at 6.5.0 call: the conditions of
/// <see cref="TemplateBlocks"/>, without its loops.
/// </summary>
[TestFixture]
public class ConditionalBlocksTests
{
    [Test]
    public void A_Loop_Does_Not_Open_A_Block()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConditionalBlocks.OpensBlock("{{#each Items}}"), Is.False);
            Assert.That(ConditionalBlocks.OpensBlock("{{#if IsPaid}}"), Is.True);
            Assert.That(ConditionalBlocks.OpensBlock(" {{ #if !IsPaid }}\r\a"), Is.True);
        });
    }

    [Test]
    public void Resolve_Removes_The_Markers_And_The_Branches_That_Do_Not_Hold()
    {
        var input = new WordTemplateInput
        {
            GlobalParameters = new Dictionary<string, object> { ["IsPaid"] = true, ["IsDue"] = false }
        };
        string?[] children =
        [
            "{{#if IsPaid}}",    // 0
            "Paid",              // 1
            "{{#if IsDue}}",     // 2
            "Due",               // 3
            "{{else}}",          // 4
            "Not due",           // 5
            "{{/if}}",           // 6
            null,                // 7: a table
            "{{else}}",          // 8
            "Unpaid",            // 9
            "{{/if}}",           // 10
            "After"              // 11
        ];

        var removed = ConditionalBlocks.Resolve(children, input);

        Assert.That(removed.Order(), Is.EqualTo(new[] { 0, 2, 3, 4, 6, 8, 9, 10 }));
    }

    [Test]
    public void A_Loop_Among_Blocks_Throws()
    {
        string?[] children = ["{{#if IsPaid}}", "{{#each Items}}", "{{Name}}", "{{/each}}", "{{/if}}"];

        Assert.Throws<FormatException>(() => ConditionalBlocks.Resolve(children, new WordTemplateInput()));
    }
}
