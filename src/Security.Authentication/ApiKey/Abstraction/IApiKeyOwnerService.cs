using Regira.Security.Authentication.ApiKey.Models;

namespace Regira.Security.Authentication.ApiKey.Abstraction;

/// <summary>
/// Finds the owner of an API key. <c>AddApiKeyAuthentication()</c> registers none: the handler resolves whichever is
/// registered, <c>AddInMemoryApiKeyAuthentication</c> registering one, and calls only <see cref="FindByKey"/>, where
/// <c>null</c> gives 401. For keys in a database, implement it over the <c>DbContext</c> and register it instead
/// (<c>services.AddScoped&lt;IApiKeyOwnerService, DbApiKeyOwnerService&gt;()</c>). Generate each key on the server from
/// 32 or more random bytes, show it once, and store and look it up by its unsalted SHA-256; revoke with a
/// <c>RevokedAt</c> that <see cref="FindByKey"/> filters on. Do not also call <c>AddInMemoryApiKeyAuthentication</c>,
/// since the last registration wins.
/// </summary>
public interface IApiKeyOwnerService
{
    public Task<ApiKeyOwner?> FindByOwner(string id);
    public Task<ApiKeyOwner?> FindByKey(string apiKey);

    public Task<bool> Validate(string id, string apiKey);
}