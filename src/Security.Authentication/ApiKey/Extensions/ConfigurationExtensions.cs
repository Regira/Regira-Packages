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
    /// Reads one owner from a configuration section. Both messages name the expected shape, because the
    /// mistake they catch is a JSON <em>structure</em> mistake: an object keyed by owner name
    /// (<c>"ApiKeys": { "client-a": { "Key": … } }</c>) binds each key as a child section with no
    /// <c>OwnerId</c>, and "OwnerId is missing" alone leaves the reader adding a field to the wrong shape.
    /// </summary>
    public static ApiKeyOwner ToApiKeyOwner(this IConfigurationSection apiKeySection)
    {
        const string expected = """expected an array of { "OwnerId": "…", "Key": "…", "Roles": [ … ] }, not an object keyed by owner""";
        return new ApiKeyOwner
        {
            Key = apiKeySection["Key"] ?? throw new InvalidOperationException($"API key is missing at configuration path \"{apiKeySection.Path}\" — {expected}."),
            OwnerId = apiKeySection["OwnerId"] ?? throw new InvalidOperationException($"OwnerId is missing at configuration path \"{apiKeySection.Path}\" — {expected}."),
            Roles = apiKeySection.GetSection("Roles").Get<List<string>>() ?? [],
            Claims = apiKeySection.GetSection("Claims").Get<List<ApiKeyOwner.Claim>>() ?? []
        };
    }
}