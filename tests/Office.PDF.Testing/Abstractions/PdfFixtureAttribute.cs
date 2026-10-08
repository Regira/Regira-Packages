using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

namespace Office.PDF.Testing.Abstractions;

/// <summary>The PDF interfaces a shared scenario calls; a backend that does not implement them all does not get it.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class NeedsAttribute(params Type[] capabilities) : Attribute
{
    public IReadOnlyList<Type> Capabilities { get; } = capabilities;
}

/// <summary>
/// A backend fixture over <see cref="PdfTestsBase{TBackend}"/>, in place of <c>[TestFixture]</c>: it builds the
/// fixture as <c>[TestFixture]</c> does, with only the shared scenarios whose <see cref="NeedsAttribute">needs</see>
/// the backend type implements. A scenario a backend cannot run is left out rather than skipped, so the fixture's
/// results list only what the backend does.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PdfFixtureAttribute : NUnitAttribute, IFixtureBuilder2
{
    private readonly NUnitTestFixtureBuilder _builder = new();

    public IEnumerable<TestSuite> BuildFrom(ITypeInfo typeInfo) => BuildFrom(typeInfo, new MatchAll());

    public IEnumerable<TestSuite> BuildFrom(ITypeInfo typeInfo, IPreFilter filter)
    {
        yield return _builder.BuildFrom(typeInfo, new Capable(filter, BackendType(typeInfo.Type)));
    }

    /// <summary>The <c>TBackend</c> a fixture passes to <see cref="PdfTestsBase{TBackend}"/>.</summary>
    private static Type BackendType(Type fixture)
    {
        for (var type = fixture; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PdfTestsBase<>))
            {
                return type.GetGenericArguments()[0];
            }
        }
        throw new InvalidOperationException($"{fixture.Name} is marked [PdfFixture] but does not derive from {nameof(PdfTestsBase<object>)}<TBackend>.");
    }

    private sealed class Capable(IPreFilter filter, Type backend) : IPreFilter
    {
        public bool IsMatch(Type type) => filter.IsMatch(type);

        public bool IsMatch(Type type, MethodInfo method)
            => filter.IsMatch(type, method)
               && (method.GetCustomAttribute<NeedsAttribute>()?.Capabilities ?? []).All(capability => capability.IsAssignableFrom(backend));
    }

    // NUnit's own empty filter is internal
    private sealed class MatchAll : IPreFilter
    {
        public bool IsMatch(Type type) => true;
        public bool IsMatch(Type type, MethodInfo method) => true;
    }
}
