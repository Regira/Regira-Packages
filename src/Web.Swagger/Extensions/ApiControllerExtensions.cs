using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace Regira.Web.Swagger.Extensions;

public static class ApiControllerExtensions
{
    public static IMvcBuilder DisplayEnumAsString(this IMvcBuilder builder)
    {
        return builder
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }
}
