using Microsoft.Extensions.DependencyInjection;
using Regira.System.Hosting.DependencyInjection;

namespace Hosting.Testing;

[TestFixture]
public class TypedServiceProviderTests
{
    #region Types
    class TypeA;
    class TypeB;
    class TypeC;
    interface ITestImplementation;
    interface IDummy;
    class ServiceA : ITestImplementation;
    class ServiceB : ITestImplementation;
    class ServiceC : IDummy;
    #endregion

    [Test]
    public void Test_TypedServiceProvider()
    {
        var services = new ServiceCollection()
            .AddTypedProvider<TypeA, ITestImplementation>(_ => new ServiceA())
            .AddTypedProvider<TypeB, ITestImplementation>(_ => new ServiceB())
            .AddTypedProvider<TypeC, IDummy>(_ => new ServiceC());

        var sp = services.BuildServiceProvider();
        var serviceA = sp.GetTypedImplementation<TypeA, ITestImplementation>();
        var serviceB = sp.GetTypedImplementation<TypeB, ITestImplementation>();
        var serviceC = sp.GetTypedImplementation<TypeC, IDummy>();

        Assert.That(serviceA, Is.AssignableTo<ITestImplementation>());
        Assert.That(serviceB, Is.AssignableTo<ITestImplementation>());
        Assert.That(serviceC, Is.AssignableTo<IDummy>());

        Assert.That(serviceA, Is.TypeOf<ServiceA>());
        Assert.That(serviceB, Is.TypeOf<ServiceB>());
        Assert.That(serviceC, Is.TypeOf<ServiceC>());
    }
    [Test]
    public void Test_TypedServiceProviders()
    {
        var services = new ServiceCollection()
            .AddTypedProvider<TypeA, ITestImplementation>(_ => new ServiceA())
            .AddTypedProvider<TypeA, ITestImplementation>(_ => new ServiceB())
            .AddTypedProvider<TypeC, IDummy>(_ => new ServiceC());
        var sp = services.BuildServiceProvider();
        var servicesA = sp.GetTypedImplementations<TypeA, ITestImplementation>()
            .ToArray();

        Assert.That(servicesA.Count(), Is.EqualTo(2));
        Assert.That(servicesA, Is.All.AssignableTo<ITestImplementation>());
    }
    [Test]
    public void Test_TypedServiceProvider_Without_Factory()
    {
        var services = new ServiceCollection()
            .AddTypedProvider<TypeA, ITestImplementation, ServiceA>()
            .AddTypedProvider<TypeB, ITestImplementation, ServiceB>()
            .AddTypedProvider<TypeC, IDummy, ServiceC>();

        var sp = services.BuildServiceProvider();
        var serviceA = sp.GetTypedImplementation<TypeA, ITestImplementation>();
        var serviceB = sp.GetTypedImplementation<TypeB, ITestImplementation>();
        var serviceC = sp.GetTypedImplementation<TypeC, IDummy>();

        Assert.That(serviceA, Is.AssignableTo<ITestImplementation>());
        Assert.That(serviceB, Is.AssignableTo<ITestImplementation>());
        Assert.That(serviceC, Is.AssignableTo<IDummy>());

        Assert.That(serviceA, Is.TypeOf<ServiceA>());
        Assert.That(serviceB, Is.TypeOf<ServiceB>());
        Assert.That(serviceC, Is.TypeOf<ServiceC>());
    }
    [Test]
    public void Test_TypedServiceProvider_Should_Return_Null()
    {
        var services = new ServiceCollection();
        // no registrations

        var sp = services.BuildServiceProvider();
        var serviceA = sp.GetTypedImplementation<TypeA, ITestImplementation>();
        var serviceB = sp.GetTypedImplementation<TypeB, ITestImplementation>();
        var serviceC = sp.GetTypedImplementation<TypeC, IDummy>();

        Assert.That(serviceA, Is.Null);
        Assert.That(serviceB, Is.Null);
        Assert.That(serviceC, Is.Null);
    }
}