using Regira.Security.Authentication.ApiKey.Models;

namespace Regira.Security.Authentication.ApiKey.Abstraction;

public interface IApiKeyOwnerService
{
    public Task<ApiKeyOwner?> FindByOwner(string id);
    public Task<ApiKeyOwner?> FindByKey(string apiKey);

    public Task<bool> Validate(string id, string apiKey);
}