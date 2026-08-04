using Regira.Normalizing;
using Regira.Normalizing.Abstractions;

namespace Normalizing.Testing.Models;

public class NormalizableObject1
{
    [Normalized]
    public string? NormalizedProp { get; set; }
}
public class NormalizableObject2
{
    public string? SourceProp { get; set; }
    [Normalized(SourceProperty = nameof(SourceProp), SourceProperties = [nameof(SourceProp)])]
    public string? NormalizedProp { get; set; }
}
public class NormalizableObject3
{
    public string? SourceProp { get; set; }
    [Normalized(SourceProperty = nameof(SourceProp), Normalizer = typeof(TestNormalizer))]
    public string? NormalizedProp { get; set; }
}
public class NormalizableObject4
{
    public string? SourceProp1 { get; set; }
    [Normalized(SourceProperty = nameof(SourceProp1))]
    public string? NormalizedProp1 { get; set; }
    public string? SourceProp2 { get; set; }
    [Normalized(SourceProperty = nameof(SourceProp2), Normalizer = typeof(TestNormalizer))]
    public string? NormalizedProp2 { get; set; }
    [Normalized(Normalizer = typeof(TestNormalizer))]
    public string? NormalizedProp3 { get; set; }
    [Normalized(SourceProperties = [nameof(SourceProp1), nameof(SourceProp2)])]
    public string? NormalizedContent { get; set; }
}

public class NestedObject
{
    //public NormalizableObject1? Obj1 { get; set; }
    public NormalizableObject2? Obj2 { get; set; }
    public NestedObject? Obj3 { get; set; }
    public ICollection<NestedObject>? Collection { get; set; }
}

public class TestNormalizer : INormalizer
{
    public string? Normalize(string? input)
        => input?.ToUpper().Replace(" ", "_");
}