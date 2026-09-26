#if NETCOREAPP3_1_OR_GREATER
using System.Text.Json.Serialization.Metadata;
using Regira.Utilities;

namespace Regira.Entities.Web.DependencyInjection;

/// <summary>
/// Reads the <see cref="DateTime"/> and <see cref="Nullable{DateTime}"/> properties of a request body under the ambient
/// UTC policy (<see cref="DateTimeDefaults.UseUtc"/>), so a bound entity or DTO already has the kind the database stores
/// it with. A client sending a local offset (<c>…T19:00:00+02:00</c>, what the Regira SPA sends) otherwise binds as
/// <see cref="DateTimeKind.Local"/>, while the stored row a prepper compares it with comes back as
/// <see cref="DateTimeKind.Utc"/> — and <see cref="DateTime"/> equality compares ticks, not instants, so on a server
/// not running in UTC an unchanged value compares as changed.
/// <para>
/// A <see cref="JsonTypeInfo"/> modifier rather than a <c>JsonConverter&lt;DateTime&gt;</c>: a custom converter hides
/// the type from the schema exporter <c>AddOpenApi()</c> relies on, which then describes every <c>DateTime</c> without
/// its <c>"type": "string"</c> (and a <c>DateTime?</c> without its null). Wrapping the property setter keeps the
/// built-in converter, so the contract is unchanged and writing is untouched. It reaches properties bound through a
/// setter (<c>set</c> or <c>init</c>) — entities and DTOs; a value bound through a constructor parameter (a positional
/// record) is read as sent, and so is a controller body read through Newtonsoft (<c>AddNewtonsoftJson</c>). It is added
/// in a <c>PostConfigure</c> around the whole resolver chain, so a source-generated context the app inserts in its own
/// <c>Configure</c> is covered; a resolver replaced in the app's own <c>PostConfigure</c> is not.
/// </para>
/// </summary>
internal static class UtcDateTimeJsonModifier
{
    public static void Apply(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (property.Set is not { } set || (property.PropertyType != typeof(DateTime) && property.PropertyType != typeof(DateTime?)))
            {
                continue;
            }
            property.Set = (target, value) => set(target, value is DateTime date && DateTimeDefaults.UseUtc ? date.AsUtc() : value);
        }
    }
}
#endif
