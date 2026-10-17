using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Web.Security.Testing.Infrastructure;

/// <summary>
/// How long the access tokens of <see cref="Jwt.JwtStartup"/> and <see cref="Identity.RefreshTokenStartup"/> live.
/// An hour by default, so a slow parallel run cannot see a token expire in the middle of a test; a test that needs
/// a token to expire asks for a copy of its host with a short lifespan.
/// </summary>
public static class TokenLifeSpan
{
    public const string Setting = "Authentication:Jwt:LifeSpan";
    public const int Default = 3600;

    /// <summary>A copy of the host whose access tokens expire after <paramref name="seconds"/>.</summary>
    public static WebApplicationFactory<TStartup> WithTokenLifeSpan<TStartup>(this WebApplicationFactory<TStartup> factory, int seconds)
        where TStartup : class
        => factory.WithWebHostBuilder(b => b.UseSetting(Setting, seconds.ToString(CultureInfo.InvariantCulture)));
}
