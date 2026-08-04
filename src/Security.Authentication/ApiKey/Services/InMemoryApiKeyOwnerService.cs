using Microsoft.Extensions.Configuration;
using Regira.Security.Authentication.ApiKey.Abstraction;
using Regira.Security.Authentication.ApiKey.Extensions;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Utilities;

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
        return Task.FromResult(_apiKeyOwners.FirstOrDefault(x => x.Key == apiKey));
    }

    public async Task<bool> Validate(string id, string apiKey)
    {
        var owner = await FindByOwner(id);
        return owner != null && owner.Key == apiKey;
    }

    public static InMemoryApiKeyOwnerService FromConfigurationSection(IConfigurationSection configSection)
        => new(configSection.ToApiKeyOwners());
}