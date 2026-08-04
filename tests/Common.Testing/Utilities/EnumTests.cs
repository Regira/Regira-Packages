using Regira.Utilities;

namespace Common.Testing.Utilities;

[Flags]
public enum TestEnum
{
    None = 0,           // 0
    One = 1 << 0,       // 1
    Two = 1 << 1,       // 2
    Three = One | Two,  // 3
    Four = 1 << 2,      // 4
    Five = One | Four,  // 5
    Seven = None | One | Two | Four, // 7
    SixtyFour = 1 << 6,
    SixtyNine = SixtyFour | Five,
    //All = ~0            // -1 -> not working as expected with integers
}

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class EnumTests
{
    private readonly TestEnum[] _validFlags = [TestEnum.None, TestEnum.One, TestEnum.Two, TestEnum.Four, TestEnum.SixtyFour
    ];
    private readonly TestEnum[] _combos = new[] { 3, 5, 6, 7, 65, 66, 67, 68, 69, 70, 71 }.Cast<TestEnum>().ToArray();

    [Test]
    public void List_Bitmask_FlagValues()
    {
        var values = EnumUtility.ListValidFlagValues<TestEnum>().ToArray();
        var values2 = EnumUtility.ListFlagValues<TestEnum>(/*false*/);
        Assert.That(values2, Is.EquivalentTo(values));
        Assert.That(values, Is.EquivalentTo(_validFlags));
    }
    [Test]
    public void List_All_FlagValues()
    {
        var values = EnumUtility.ListFlagValues<TestEnum>(true).ToArray();
        var expected = _validFlags.Concat(_combos);
        Assert.That(values, Is.EquivalentTo(expected));
    }
    [Test]
    public void List_Also_Undefined_FlagValues()
    {
        var values = EnumUtility.ListFlagValues<TestEnum>(true, false).ToArray();
        var maxValue = (int)EnumUtility.GetMaxFlagValue<TestEnum>();
        var expected = Enumerable.Range(0, maxValue + 1).Select(v => (TestEnum)v);
        Assert.That(values, Is.EquivalentTo(expected));
    }

    [Test]
    public void Only_Valid_Flag_Values_Are_Included()
    {
        var allCombinations = EnumUtility.ListValidFlagValues<TestEnum>()
            .ToArray();
        Assert.That(allCombinations, Has.Member(TestEnum.One));
        Assert.That(allCombinations, Has.Member(TestEnum.Two));
        Assert.That(allCombinations, Has.Member(TestEnum.Four));

        Assert.That(allCombinations, Has.Member(TestEnum.None));
        var firstAndSecond = TestEnum.One | TestEnum.Two;
        Assert.That(allCombinations, Has.No.Member(firstAndSecond));
        var secondAndThird = TestEnum.Two | TestEnum.Four;
        Assert.That(allCombinations, Has.No.Member(secondAndThird));
        var firstAndThird = TestEnum.One | TestEnum.Four;
        Assert.That(allCombinations, Has.No.Member(firstAndThird));
        Assert.That(allCombinations, Has.No.Member(TestEnum.Three));
        Assert.That(allCombinations, Has.No.Member(TestEnum.Seven));
        Assert.That(allCombinations, Has.No.Member(TestEnum.SixtyNine));

        Assert.That(allCombinations, Has.No.Member((TestEnum)8));
    }
    [Test]
    public void All_Combinations_Are_Included()
    {
        var allCombinations = EnumUtility.ListFlagValues<TestEnum>(true)
            .ToArray();
        Assert.That(allCombinations, Has.Member(TestEnum.None));
        Assert.That(allCombinations, Has.Member(TestEnum.One));
        Assert.That(allCombinations, Has.Member(TestEnum.Two));
        Assert.That(allCombinations, Has.Member(TestEnum.Four));
        var firstAndSecond = TestEnum.One | TestEnum.Two;
        Assert.That(allCombinations, Has.Member(firstAndSecond));
        var secondAndThird = TestEnum.Two | TestEnum.Four;
        Assert.That(allCombinations, Has.Member(secondAndThird));
        var firstAndThird = TestEnum.One | TestEnum.Four;
        Assert.That(allCombinations, Has.Member(firstAndThird));
        Assert.That(allCombinations, Has.Member(TestEnum.Three));
        Assert.That(allCombinations, Has.Member(TestEnum.Seven));
        Assert.That(allCombinations, Has.Member(TestEnum.SixtyNine));
    }

    [Test]
    public void Max_FlagValue_Equals_SumOfAll()
    {
        var maxValue = (int)EnumUtility.GetMaxFlagValue<TestEnum>();
        var all = TestEnum.One | TestEnum.Two | TestEnum.Four | TestEnum.SixtyFour;
        Assert.That(maxValue, Is.EqualTo((int)(all)));
    }
    [Test]
    public void Max_FlagValue()
    {
        var maxValue = EnumUtility.GetMaxFlagValue<TestEnum>();
        Assert.That(maxValue.HasFlag(TestEnum.None | TestEnum.One | TestEnum.Two | TestEnum.Four), Is.True);
    }
}