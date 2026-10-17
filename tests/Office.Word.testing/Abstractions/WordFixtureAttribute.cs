using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// What a shared scenario in <see cref="WordTestsBase"/> needs from a backend, beyond reading .docx templates and
/// filling <c>{{ key }}</c> parameters.
/// </summary>
[Flags]
public enum WordFeature
{
    None = 0,
    /// <summary><c>IWordCreator</c></summary>
    Creating = 1 << 0,
    /// <summary><c>IWordConverter</c></summary>
    Converting = 1 << 1,
    /// <summary><c>IWordMerger</c></summary>
    Merging = 1 << 2,
    /// <summary><c>IWordTextExtractor</c></summary>
    TextExtraction = 1 << 3,
    /// <summary><c>IWordImageExtractor</c></summary>
    ImageExtraction = 1 << 4,
    /// <summary><c>IWordToImagesService</c></summary>
    PageImages = 1 << 5,
    /// <summary><c>DocumentParameters</c> inserted at <c>&lt;{ key }&gt;</c></summary>
    NestedDocuments = 1 << 6,
    /// <summary><c>Headers</c> and <c>Footers</c></summary>
    HeadersAndFooters = 1 << 7,
    /// <summary>Non-default <c>InputOptions</c></summary>
    InputOptions = 1 << 8,
    /// <summary><c>CollectionParameters</c> filling the table whose Alt Text title is the key</summary>
    TitledTables = 1 << 9,
    /// <summary><c>Images</c> replacing the picture whose Alt Text is the image's name</summary>
    AltTextPictures = 1 << 10,
    /// <summary>Parameters filling the text after a bookmark of that name, in a binary <c>.dot</c> template</summary>
    Bookmarks = 1 << 11,
    /// <summary>The backend's <c>DocumentBuilder</c></summary>
    DocumentBuilder = 1 << 12,
    /// <summary>Parameters whose key starts with <c>html_</c>, converted from HTML</summary>
    HtmlParameters = 1 << 13,
    /// <summary><c>Convert</c> to formats other than PDF</summary>
    OtherFormats = 1 << 14
}

/// <summary>The features a shared scenario needs; a fixture that leaves one of them out does not get the scenario.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class NeedsAttribute(WordFeature features) : Attribute
{
    public WordFeature Features { get; } = features;
}

/// <summary>
/// Features a backend fixture does not have, and why. The shared scenarios that need one are left out of the fixture
/// rather than skipped, so its results list only what the backend does.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class LeavesOutAttribute(WordFeature features, string reason) : Attribute
{
    public WordFeature Features { get; } = features;
    public string Reason { get; } = reason;

    public static WordFeature Of(Type fixture)
        => fixture.GetCustomAttributes<LeavesOutAttribute>().Aggregate(WordFeature.None, (all, attribute) => all | attribute.Features);
}

/// <summary>
/// A backend fixture over <see cref="WordTestsBase"/>, in place of <c>[TestFixture]</c>: it builds the fixture as
/// <c>[TestFixture]</c> does, without the shared scenarios that need a feature the fixture
/// <see cref="LeavesOutAttribute">leaves out</see>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WordFixtureAttribute : NUnitAttribute, IFixtureBuilder2
{
    private readonly NUnitTestFixtureBuilder _builder = new();

    public IEnumerable<TestSuite> BuildFrom(ITypeInfo typeInfo) => BuildFrom(typeInfo, new MatchAll());

    public IEnumerable<TestSuite> BuildFrom(ITypeInfo typeInfo, IPreFilter filter)
    {
        yield return _builder.BuildFrom(typeInfo, new LeaveOut(filter, LeavesOutAttribute.Of(typeInfo.Type)));
    }

    private sealed class LeaveOut(IPreFilter filter, WordFeature leftOut) : IPreFilter
    {
        public bool IsMatch(Type type) => filter.IsMatch(type);

        public bool IsMatch(Type type, MethodInfo method)
            => filter.IsMatch(type, method) && ((method.GetCustomAttribute<NeedsAttribute>()?.Features ?? WordFeature.None) & leftOut) == WordFeature.None;
    }

    // NUnit's own empty filter is internal
    private sealed class MatchAll : IPreFilter
    {
        public bool IsMatch(Type type) => true;
        public bool IsMatch(Type type, MethodInfo method) => true;
    }
}
