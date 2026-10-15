using Microsoft.Extensions.Configuration;
using Regira.Security.Authentication.ApiKey.Models;

namespace Regira.Security.Authentication.ApiKey.Extensions;

public static class ConfigurationExtensions
{
    public static IList<ApiKeyOwner> ToApiKeyOwners(this IConfigurationSection apiKeysSection)
    {
        return apiKeysSection
            .GetChildren()
            .Select(ToApiKeyOwner)
            .ToList();
    }
    /// <summary>
    /// Reads one owner from a configuration section. A missing <c>OwnerId</c> names the expected shape, because
    /// the mistake it catches is a JSON <em>structure</em> mistake: an object keyed by owner name
    /// (<c>"ApiKeys": { "client-a": { "Key": … } }</c>) binds each key as a child section with no
    /// <c>OwnerId</c>, and "OwnerId is missing" alone leaves the reader adding a field to the wrong shape.
    /// A missing <c>Key</c> beside an <c>OwnerId</c> is a secret that was never set, since keys stay out of
    /// <c>appsettings.json</c>: that message names the path to set.
    /// </summary>
    public static ApiKeyOwner ToApiKeyOwner(this IConfigurationSection apiKeySection)
    {
        const string expected = """expected an array of { "OwnerId": "…", "Key": "…", "Roles": [ … ] }, not an object keyed by owner""";
        var ownerId = apiKeySection["OwnerId"]
            ?? throw new InvalidOperationException($"OwnerId is missing at configuration path \"{apiKeySection.Path}\" — {expected}.");
        var key = apiKeySection["Key"]
            ?? throw new InvalidOperationException(
                $"API key is missing at configuration path \"{apiKeySection.Path}:Key\" (owner \"{ownerId}\"). Set it through user-secrets " +
                $"in development and a key vault or the environment ({apiKeySection.Path.Replace(":", "__")}__Key) in production.");
        return new ApiKeyOwner
        {
            Key = key,
            OwnerId = ownerId,
            Roles = apiKeySection.GetSection("Roles").Get<List<string>>() ?? [],
            Claims = apiKeySection.GetSection("Claims").Get<List<ApiKeyOwner.Claim>>() ?? []
        };
    }
}