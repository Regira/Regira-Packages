using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Core.Extensions;
using Regira.Security.Authentication.Jwt.Extensions;
using Shouldly;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// Registration mistakes that would otherwise surface as a runtime 500 or as silently wrong behaviour. Each of these
/// is a failure a host should hit while starting, not on a request.
/// </summary>
public class AuthenticationRegistrationTests
{
    private const string Secret = "registration-tests-secret-long-enough-for-the-hs512-default-01234567";

    /// <summary>
    /// ⚠️ Refresh tokens renew a token this application <em>issues</em>, so they need the issuing scheme.
    /// <c>AddBearerAuthentication</c> registers neither <c>ITokenHelper</c> nor <c>JwtTokenOptions</c> by design.
    /// Registered anyway, <c>IRefreshTokenService</c> would be unconstructable and the refresh endpoint would answer
    /// <c>500</c> — contradicting its documented "404 when not registered" contract.
    /// </summary>
    [Fact]
    public void Test_Refresh_Tokens_Without_The_Issuing_Scheme_Throws_At_Registration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddBearerAuthentication(o =>
            {
                o.Authority = "https://issuer.example";
                o.Audience = "api";
            }).AddRefreshTokens());

        exception.Message.ShouldContain("AddJwtAuthentication");
    }

    [Fact]
    public void Test_Refresh_Tokens_On_The_Jwt_Scheme_Registers_Cleanly()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Should.NotThrow(() => services.AddJwtAuthentication(o => o.Secret = Secret).AddRefreshTokens());
    }

    /// <summary>
    /// A second <c>AddSchemeSelector</c> would leave two options instances registered, with the expander reading
    /// whichever won — so the schemes a security document describes need not be the ones the selector forwards to.
    /// Failing loudly beats resolving it arbitrarily.
    /// </summary>
    [Fact]
    public void Test_Registering_The_Scheme_Selector_Twice_Throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddJwtAuthentication(o => o.Secret = Secret)
                .AddSchemeSelector()
                .AddSchemeSelector());

        exception.Message.ShouldContain("more than once");
    }
}
