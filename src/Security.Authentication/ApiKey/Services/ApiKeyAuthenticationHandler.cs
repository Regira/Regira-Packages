using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.ApiKey.Abstraction;
using Regira.Security.Authentication.ApiKey.Models;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Regira.Security.Authentication.ApiKey.Services;

public class ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, IApiKeyOwnerService apiKeyOwnerService)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    // https://josef.codes/asp-net-core-protect-your-api-with-api-keys/
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (apiKeyHeaderValues.Count == 0 || string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var existingApiKey = await apiKeyOwnerService.FindByKey(providedApiKey);
        if (existingApiKey != null)
        {
            var claims = new List<Claim>
            {
                new (ClaimTypes.NameIdentifier, existingApiKey.OwnerId)
            };

            claims.AddRange(existingApiKey.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            claims.AddRange(existingApiKey.Claims.Select(c => new Claim(c.Type, c.Value)));

            var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
            var identities = new List<ClaimsIdentity> { identity };
            var principal = new ClaimsPrincipal(identities);
            var ticket = new AuthenticationTicket(principal, Options.Scheme);

            return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.NoResult();
    }
}