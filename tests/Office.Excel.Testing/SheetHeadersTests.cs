using Regira.Office.Excel.Internal;

namespace Office.Excel.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class SheetHeadersTests
{
    /// <summary>
    /// An uploaded workbook decides its headers: one header cell repeated across every column must be keyed in linear
    /// time, which took seconds at eight thousand columns.
    /// </summary>
    [Test]
    public void A_Header_Repeated_In_Every_Column_Is_Keyed_In_Linear_Time()
    {
        var cells = Enumerable.Repeat<object?>("Name", 16_384).ToArray();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var keys = SheetHeaders.Keys(cells);

        Assert.Multiple(() =>
        {
            Assert.That(keys[..3], Is.EqualTo(new[] { "Name", "Name_2", "Name_3" }).AsCollection);
            Assert.That(keys[^1], Is.EqualTo("Name_16384"));
            Assert.That(keys.Distinct(StringComparer.InvariantCultureIgnoreCase).Count(), Is.EqualTo(cells.Length));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        });
    }

    // a repeat continues where the key's previous one stopped, and still takes the first free suffix: one a header
    // spells itself is skipped, whatever its case
    [Test]
    public void A_Repeat_Takes_The_First_Free_Suffix()
    {
        var keys = SheetHeaders.Keys(["Value", "value", "Value_3", "VALUE", "Value", null]);

        Assert.That(keys, Is.EqualTo(new[] { "Value", "value_2", "Value_3", "VALUE_4", "Value_5", "Column6" }).AsCollection);
    }
}
