#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Web.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Regira.Entities.Web.DependencyInjection;

public static class EntityServiceCollectionJsonExtensions
{
    /// <summary>
    /// <list type="bullet">
    /// <item>Ignore nulls</item>
    /// <item>Ignore reference cycles</item>
    /// <item>Enums as string</item>
    /// <item>Incoming <see cref="DateTime"/> properties normalized to UTC under the ambient policy
    /// (<c>DateTimeDefaults.UseUtc</c>), before any prepper sees them — schema and wire format unchanged</item>
    /// </list>
    /// Applied to both the MVC options and the <c>Http.Json</c> options. The second set governs minimal-API
    /// results (<c>Results.Ok(...)</c>, <c>TypedResults.Json</c>) <b>and</b> is what <c>AddOpenApi()</c> reads
    /// when it generates schemas — without it the document would describe enums as integers while controllers
    /// serialize them as names, a mismatch nothing reports and one that reaches the SPA as wrong generated
    /// types. An app that already had minimal endpoints will see their payloads pick up these settings.
    /// <para>
    /// <paramref name="configure"/> customizes the MVC options, <paramref name="configureHttp"/> the
    /// <c>Http.Json</c> ones. A converter added to only one of them re-creates the same document/response
    /// mismatch, so apply contract-affecting changes to both.
    /// </para>
    /// Also calls <see cref="EntityServiceCollectionExceptionExtensions.MapEntityExceptions"/>: the entity
    /// exceptions belong to the same HTTP contract as the JSON shape, so a hand-written domain action and the
    /// generated write actions answer a rule breach alike.
    /// <para>
    /// Startup validation of entity controllers is enabled by <c>UseEntities()</c> (Development-only by
    /// default) — see <see cref="EntityControllerValidationExtensions.ValidateEntityControllers(IServiceCollection)"/> to enable it explicitly.
    /// </para>
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">Customizes the MVC JSON options.</param>
    /// <param name="configureHttp">Customizes the minimal-API / OpenAPI JSON options.</param>
    /// <returns></returns>
    public static IServiceCollection ConfigureDefaultJsonOptions(this IServiceCollection services,
        Action<JsonOptions>? configure = null, Action<HttpJsonOptions>? configureHttp = null)
    {
        services
            .Configure<JsonOptions>(o =>
            {
                ApplyDefaults(o.JsonSerializerOptions);
                configure?.Invoke(o);
            })
            .Configure<HttpJsonOptions>(o =>
            {
                ApplyDefaults(o.SerializerOptions);
                configureHttp?.Invoke(o);
            })
            // after every Configure: a source-generated context the app inserts into the resolver chain
            // (TypeInfoResolverChain.Insert(0, AppJsonContext.Default)) is wrapped too, instead of routing its
            // types around the UTC read
            .PostConfigure<JsonOptions>(o => AddUtcRead(o.JsonSerializerOptions))
            .PostConfigure<HttpJsonOptions>(o => AddUtcRead(o.SerializerOptions))
            .MapEntityExceptions();

        return services;
    }

    private static void ApplyDefaults(JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.Converters.Add(new JsonStringEnumConverter());
    }

    // a modifier, not a converter: a custom DateTime converter blanks the type out of the OpenAPI schema
    private static void AddUtcRead(JsonSerializerOptions options)
        => options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(UtcDateTimeJsonModifier.Apply);
}
#endif
