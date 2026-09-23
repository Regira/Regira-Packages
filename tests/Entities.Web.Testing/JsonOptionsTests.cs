using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Entities.Web.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Schema;
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

    // A prepper compares the bound entity with the stored row. The SPA sends a local offset, which System.Text.Json
    // binds as Kind.Local in the server's zone, while the stored row reads back as Kind.Utc — and DateTime equality
    // compares ticks, so an unchanged value looked changed on every save. The EF converter normalizes only on the
    // way to the database, after every prepper.
    private class Slot
    {
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
    }

    // The standard .NET 8+ way to add a source-generated context — inserted into the resolver chain by the app's own
    // configuration, after ConfigureDefaultJsonOptions() — must not route its types around the UTC read.
    [Fact]
    public void A_Source_Generated_Context_Added_Afterwards_Still_Gets_The_Utc_Read()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.ConfigureDefaultJsonOptions();
        services.Configure<HttpJsonOptions>(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, SlotJsonContext.Default));
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        var slot = JsonSerializer.Deserialize<GeneratedSlot>("""{"start":"2026-09-22T19:00:00+02:00"}""", options)!;

        Assert.Equal(DateTimeKind.Utc, slot.Start.Kind);
        Assert.Equal(new DateTime(2026, 9, 22, 17, 0, 0, DateTimeKind.Utc), slot.Start);
    }

    // AddOpenApi() builds its schemas through System.Text.Json's schema exporter, which cannot see through a custom
    // converter: a JsonConverter<DateTime> turned every DateTime into an untyped `{"format":"date-time"}` and dropped
    // the nullability of DateTime? — in every generated client. The UTC read must leave the contract alone.
    [Fact]
    public void The_Utc_Read_Leaves_The_DateTime_Schema_Intact()
    {
        using var sp = Build();
        var options = sp.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(Slot))["properties"]!;

        Assert.Equal("string", schema["start"]!["type"]!.GetValue<string>());
        Assert.Equal("date-time", schema["start"]!["format"]!.GetValue<string>());
        Assert.Equal("""["string","null"]""", schema["end"]!["type"]!.ToJsonString());
    }

    [Fact]
    public void Offset_DateTime_Binds_As_The_Same_Instant_In_Utc()
    {
        using var sp = Build();
        var options = sp.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions;

        var slot = JsonSerializer.Deserialize<Slot>("""{"start":"2026-09-22T19:00:00.000+02:00","end":"2026-09-22T20:30:00+02:00"}""", options)!;

        Assert.Equal(DateTimeKind.Utc, slot.Start.Kind);
        Assert.Equal(new DateTime(2026, 9, 22, 17, 0, 0, DateTimeKind.Utc), slot.Start);
        Assert.Equal(DateTimeKind.Utc, slot.End!.Value.Kind);
        Assert.Equal(new DateTime(2026, 9, 22, 18, 30, 0, DateTimeKind.Utc), slot.End);
    }

    [Fact]
    public void Offsetless_DateTime_Is_Taken_As_Utc_And_Utc_Writes_Back_Unchanged()
    {
        using var sp = Build();
        var options = sp.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        var slot = JsonSerializer.Deserialize<Slot>("""{"start":"2026-09-22T17:00:00","end":null}""", options)!;

        Assert.Equal(new DateTime(2026, 9, 22, 17, 0, 0, DateTimeKind.Utc), slot.Start);
        Assert.Equal(DateTimeKind.Utc, slot.Start.Kind);
        Assert.Null(slot.End);
        Assert.Equal("""{"start":"2026-09-22T17:00:00Z"}""", JsonSerializer.Serialize(slot, options));
    }

    [Fact]
    public void Caller_Customization_Applies_To_The_Mvc_Options()
    {
        using var sp = Build(o => o.JsonSerializerOptions.WriteIndented = true);

        Assert.True(sp.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions.WriteIndented);
    }
}

public class GeneratedSlot
{
    public DateTime Start { get; set; }
}

[JsonSerializable(typeof(GeneratedSlot))]
internal partial class SlotJsonContext : JsonSerializerContext;
