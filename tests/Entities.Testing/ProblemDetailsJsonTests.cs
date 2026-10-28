using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Entities.Web.DependencyInjection;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Entities.Testing;

/// <summary>
/// <c>ConfigureDefaultJsonOptions()</c> over a resolver chain that already holds a resolver: <c>AddProblemDetails()</c>'s,
/// as the web API templates register it, or a source-generated context the app inserts. Here rather than in
/// Entities.Web.Testing because the project runs on net8.0 too: on .NET 8, <c>TypeInfoResolver</c> then returns the chain
/// itself, and a UTC read that wrapped it and assigned it back would make the chain contain itself, so the first
/// serialization would overflow the stack and end the process.
/// </summary>
[TestFixture]
public class ProblemDetailsJsonTests
{
    public class Slot
    {
        public DateTime Start { get; set; }
    }

    [TestCase(true, TestName = "Problem details registered before the JSON options")]
    [TestCase(false, TestName = "Problem details registered after the JSON options")]
    public void A_Problem_Serializes_And_The_Utc_Read_Still_Applies(bool problemDetailsFirst)
    {
        var services = new ServiceCollection();
        if (problemDetailsFirst)
        {
            services.AddProblemDetails();
        }
        services.ConfigureDefaultJsonOptions();
        if (!problemDetailsFirst)
        {
            services.AddProblemDetails();
        }
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        var problem = JsonSerializer.Serialize(new ProblemDetails { Status = 500, Title = "An error occurred" }, options);
        var slot = JsonSerializer.Deserialize<Slot>("""{"start":"2026-09-22T19:00:00+02:00"}""", options)!;

        Assert.That(problem, Does.Contain("\"status\":500"));
        Assert.That(slot.Start, Is.EqualTo(new DateTime(2026, 9, 22, 17, 0, 0, DateTimeKind.Utc)));
        Assert.That(slot.Start.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    // the insert ConfigureDefaultJsonOptions' own comment shows, with no problem details in the host
    [Test]
    public void A_Source_Generated_Context_Inserted_Into_The_Chain_Gets_The_Utc_Read()
    {
        var services = new ServiceCollection();
        services.ConfigureDefaultJsonOptions(configureHttp: o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, SlotJsonContext.Default));
        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

        var slot = JsonSerializer.Deserialize<Slot>("""{"start":"2026-09-22T19:00:00+02:00"}""", options)!;

        Assert.That(slot.Start, Is.EqualTo(new DateTime(2026, 9, 22, 17, 0, 0, DateTimeKind.Utc)));
        Assert.That(slot.Start.Kind, Is.EqualTo(DateTimeKind.Utc));
    }
}

[JsonSerializable(typeof(ProblemDetailsJsonTests.Slot))]
internal partial class SlotJsonContext : JsonSerializerContext;
