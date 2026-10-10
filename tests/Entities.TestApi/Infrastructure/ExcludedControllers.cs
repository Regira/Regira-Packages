using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Entities.TestApi.Infrastructure;

/// <summary>Removes controllers from the ones MVC discovers, for a host that serves their routes another way.</summary>
public class ExcludedControllers(params Type[] controllers) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(c => controllers.Contains(c.AsType())).ToList())
        {
            feature.Controllers.Remove(controller);
        }
    }
}
