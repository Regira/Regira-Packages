using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Entities.Web.DependencyInjection;
using System.Text.Json.Serialization;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace Entities.Web.Testing;

// AddOpenApi() generates schemas from the Http.Json options, not the MVC ones. Configuring only MVC gives a
// document that types enums as integers while controllers send names — invisible server-side, and it reaches
// the SPA as wrong generated types.
public class JsonOptionsTests
{
    private static ServiceProvider Build(Action<MvcJsonOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.ConfigureDefaultJsonOptions(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Defaults_Reach_The_Options_OpenApi_Reads()
    {
        using var sp = Build();
        var options = sp.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        Assert.NotEmpty(options.Converters.OfType<JsonStringEnumConverter>());
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        Assert.Equal(ReferenceHandler.IgnoreCycles, options.ReferenceHandler);
    }

    [Fact]
    public void Defaults_Still_Apply_To_The_Mvc_Options()
    {
        using var sp = Build();
        var options = sp.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;

        Assert.NotEmpty(options.Converters.OfType<JsonStringEnumConverter>());
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        Assert.Equal(ReferenceHandler.IgnoreCycles, options.ReferenceHandler);
    }

    [Fact]
    public void Caller_Customization_Applies_To_The_Mvc_Options()
    {
        using var sp = Build(o => o.JsonSerializerOptions.WriteIndented = true);

        Assert.True(sp.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions.WriteIndented);
    }
}
