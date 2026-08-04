using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;

namespace Regira.Security.Authentication.Jwt.Extensions;

public static class RefreshTokenExtensions
{
    /// <summary>
    /// Adds rotating refresh tokens to the JWT scheme. Chains off
    /// <see cref="JwtAuthenticationServiceCollectionExtensions.AddJwtAuthentication"/>, the way
    /// <c>AddInMemoryApiKeyAuthentication</c> chains off <c>AddApiKeyAuthentication</c>.
    /// <para>
    /// This is opt-in, and nothing changes for a host that does not call it: without an
    /// <see cref="IRefreshTokenService"/> registered, sign-in returns exactly the response it returned before refresh
    /// tokens existed and the refresh endpoint answers <c>404</c>.
    /// </para>
    /// <para>
    /// ⚠️ Uses <see cref="InMemoryRefreshTokenStore"/> unless a store is already registered — which is fine for
    /// development and wrong everywhere else: it loses every session on restart and is per-process, so behind a load
    /// balancer a refresh lands on an instance that has never heard of the token. Register an
    /// <see cref="IRefreshTokenStore"/> over your own <c>DbContext</c> before this call.
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddRefreshTokens(this AuthenticationBuilder builder, Action<RefreshTokenOptions>? configure = null)
    {
        var options = new RefreshTokenOptions();
        configure?.Invoke(options);

        // Refresh tokens renew an access token this application *issues*, so they need the issuing scheme.
        // AddBearerAuthentication registers neither ITokenHelper nor JwtTokenOptions by design — it validates
        // tokens signed elsewhere. Caught here rather than left to DI: registering an unconstructable service would
        // surface as a 500 from the refresh endpoint, contradicting its documented 404-when-absent contract.
        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ITokenHelper))
            || !builder.Services.Any(descriptor => descriptor.ServiceType == typeof(JwtTokenOptions)))
        {
            throw new InvalidOperationException(
                $"{nameof(AddRefreshTokens)} requires the self-issued JWT scheme — call it on the builder returned " +
                $"by AddJwtAuthentication. It renews an access token this application mints, so it needs " +
                $"{nameof(ITokenHelper)} and {nameof(JwtTokenOptions)}; AddBearerAuthentication registers neither, " +
                "because that scheme validates tokens an external authority signed rather than issuing any.");
        }

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        builder.Services.TryAddScoped<IRefreshTokenService, RefreshTokenService>();

        return builder;
    }

    /// <inheritdoc cref="AddRefreshTokens(AuthenticationBuilder,Action{RefreshTokenOptions})"/>
    public static AuthenticationBuilder AddRefreshTokens(this AuthenticationBuilder builder, IConfiguration configuration)
        => builder.AddRefreshTokens(options =>
            configuration.GetSection(AuthenticationSections.RefreshTokens).Bind(options));

    /// <summary>
    /// Registers a refresh-token store. Call before <see cref="AddRefreshTokens(AuthenticationBuilder,Action{RefreshTokenOptions})"/>,
    /// which only falls back to the in-memory store when none is registered.
    /// </summary>
    public static AuthenticationBuilder AddRefreshTokenStore<TStore>(this AuthenticationBuilder builder)
        where TStore : class, IRefreshTokenStore
    {
        builder.Services.AddScoped<IRefreshTokenStore, TStore>();
        return builder;
    }
}
