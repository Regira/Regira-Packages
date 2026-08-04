using Regira.Normalizing.Abstractions;

namespace Regira.Normalizing;

/// <summary>
/// Indicates that a property should be in a normalized form.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class NormalizedAttribute : Attribute
{
    /// <summary>
    /// Only valid on Property
    /// </summary>
    public string? SourceProperty { get; set; }
    /// <summary>
    /// Only valid on Property<br />
    /// Content of different SourceProperties will be concatenated with a space
    /// </summary>
    public string[]? SourceProperties { get; set; }

    /// <summary>
    /// Only valid on class
    /// </summary>
    public bool Recursive { get; set; } = true;
    /// <summary>
    /// Should implement interface <see cref="INormalizer"/> for properties or <see cref="IObjectNormalizer" /> for classes
    /// </summary>
    public Type? Normalizer { get; set; }
}