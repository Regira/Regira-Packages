using Microsoft.Extensions.Configuration;
using Regira.Security.Authentication.ApiKey.Abstraction;
using Regira.Security.Authentication.ApiKey.Extensions;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Utilities;
using System.Security.Cryptography;
using System.Text;

namespace Regira.Security.Authentication.ApiKey.Services;

public class InMemoryApiKeyOwnerService(IEnumerable<ApiKeyOwner> apiKeyOwners) : IApiKeyOwnerService
{
    private readonly IList<ApiKeyOwner> _apiKeyOwners = apiKeyOwners.AsList();

    public Task<ApiKeyOwner?> FindByOwner(string id)
    {
        return Task.FromResult(_apiKeyOwners.FirstOrDefault(x => x.OwnerId.Equals(id, StringComparison.InvariantCultureIgnoreCase)));
    }
    public Task<ApiKeyOwner?> FindByKey(string apiKey)
    {
        // every key is compared, so the response time shows neither how much of a key matched nor where it is listed
        ApiKeyOwner? match = null;
        foreach (var owner in _apiKeyOwners)
        {
            if (KeyEquals(owner.Key, apiKey) && match == null)
            {
                match = owner;
            }
        }
        return Task.FromResult(match);
    }

    public async Task<bool> Validate(string id, string apiKey)
    {
        var owner = await FindByOwner(id);
        return owner != null && KeyEquals(owner.Key, apiKey);
    }

    /// <summary>
    /// Compares in constant time: both keys are hashed first, so their lengths do not show either.
    /// </summary>
    private static bool KeyEquals(string? expected, string? provided)
        => expected != null && provided != null
           && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(provided)));

    public static InMemoryApiKeyOwnerService FromConfigurationSection(IConfigurationSection configSection)
        => new(configSection.ToApiKeyOwners());
}