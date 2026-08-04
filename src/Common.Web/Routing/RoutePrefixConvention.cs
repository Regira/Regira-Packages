using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Regira.Web.Routing;

public class RoutePrefixConvention(IRouteTemplateProvider routeTemplateProvider) : IApplicationModelConvention
{
    // https://stackoverflow.com/questions/63343735/asp-net-core-3-adding-route-prefix#63470358
    private readonly AttributeRouteModel _centralPrefix = new(routeTemplateProvider);

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var matchedSelectors = controller.Selectors
                .Where(x => x.AttributeRouteModel != null)
                .ToList();
            if (matchedSelectors.Any())
            {
                foreach (var selectorModel in matchedSelectors)
                {
                    selectorModel.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(_centralPrefix, selectorModel.AttributeRouteModel);
                }
            }

            var unmatchedSelectors = controller.Selectors
                .Where(x => x.AttributeRouteModel == null)
                .ToList();
            if (unmatchedSelectors.Any())
            {
                foreach (var selectorModel in unmatchedSelectors)
                {
                    selectorModel.AttributeRouteModel = _centralPrefix;
                }
            }
        }
    }
}
public static class RoutePrefixConventionExtensions
{
    /// <summary>
    /// services.AddControllers(options => {
    /// 	options.UseCentralRoutePrefix(new RouteAttribute("{prefix}"));
    /// });
    /// </summary>
    /// <param name="opts"></param>
    /// <param name="routeAttribute"></param>
    public static void UseCentralRoutePrefix(this MvcOptions opts, IRouteTemplateProvider routeAttribute)
    {
        opts.Conventions.Insert(0, new RoutePrefixConvention(routeAttribute));
    }
}