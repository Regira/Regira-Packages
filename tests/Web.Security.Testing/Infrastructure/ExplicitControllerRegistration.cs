using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Web.Security.Testing.Infrastructure;

/// <summary>
/// The test assembly hosts several independent startups (JWT, API-key, Identity), each mapping its own
/// controllers. Because MVC discovers every controller in the assembly, two of them share the same
/// route (<c>auth</c>) and would clash. Restricting each host to an explicit set of controller types
/// keeps the hosts isolated.
/// </summary>
public class ExplicitControllerFeatureProvider(params Type[] allowed) : ControllerFeatureProvider
{
    private readonly HashSet<Type> _allowed = [.. allowed];
    protected override bool IsController(TypeInfo typeInfo)
        => _allowed.Contains(typeInfo.AsType()) && base.IsController(typeInfo);
}

public static class ExplicitControllerRegistration
{
    public static IMvcBuilder AddControllersFor(this IServiceCollection services, params Type[] controllers)
        => services
            .AddControllers()
            .ConfigureApplicationPartManager(apm =>
            {
                foreach (var provider in apm.FeatureProviders.OfType<ControllerFeatureProvider>().ToList())
                {
                    apm.FeatureProviders.Remove(provider);
                }
                apm.FeatureProviders.Add(new ExplicitControllerFeatureProvider(controllers));
            });
}
