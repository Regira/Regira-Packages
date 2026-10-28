using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;

namespace Entities.DependencyInjection.Testing;

/// <summary>The diagnostics name a type as it is written in C#, never with the CLR's arity suffix.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class EntityServiceDiagnosticsTests
{
    [Test]
    public void FormatTypeArgs_Writes_Nested_Arguments_As_Written()
        => Assert.That(EntityServiceDiagnostics.FormatTypeArgs(typeof(IEntityService<Course, int, SearchObject<int>>)),
            Is.EqualTo("<Course, Int32, SearchObject<Int32>>"));

    [Test]
    public void FormatTypeArgs_Is_Empty_For_A_Type_That_Is_Not_Generic()
        => Assert.That(EntityServiceDiagnostics.FormatTypeArgs(typeof(Course)), Is.Empty);
}
