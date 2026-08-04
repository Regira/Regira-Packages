using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;
using Shouldly;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// Pins the behaviour of <see cref="JwtAuthenticationServiceCollectionExtensions.AddJwtAuthentication"/> and
/// <see cref="JwtTokenHelper"/> as it actually is, not as it reads.
/// <para>
/// These are characterization tests: each one describes a quirk that is load-bearing for tokens already in
/// circulation, and that a well-intentioned tidy-up of the shared <see cref="JwtBearerOptions"/> path would
/// change without any other test noticing. A failure here is not necessarily a bug — it means a deliberate
/// behaviour change, which needs a version note rather than a green build.
/// </para>
/// </summary>
public class JwtOptionsCharacterizationTests
{
    /// <summary>A 66-character secret — over the 64 bytes HS512 requires — carrying non-ASCII letters.</summary>
    private const string AccentedSecret = "régira-caractères-non-ascii-secret-pour-signature-hs512-0123456789";

    /// <summary>
    /// ⚠️ The secret is read with <see cref="Encoding.ASCII"/>, which maps every non-ASCII character to
    /// <c>?</c> (0x3F). Two secrets differing only outside ASCII therefore produce the *same* signing key, and
    /// a token issued under one validates under the other.
    /// <para>
    /// This test exists to make the obvious "fix" — switching to UTF-8 — fail loudly. That change is not
    /// backward compatible: it derives a different key from the same configured secret, so every token already
    /// issued stops validating and every session is signed out on deploy. If the encoding is ever widened, it
    /// has to be opt-in per scheme, not a silent correction.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Test_Secret_Is_Read_As_Ascii_So_Non_Ascii_Characters_Collapse()
    {
        // What Encoding.ASCII actually preserves of the secret: accents become '?'.
        var asciiFolded = Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(AccentedSecret));

        asciiFolded.ShouldNotBe(AccentedSecret);
        asciiFolded.ShouldBe("r?gira-caract?res-non-ascii-secret-pour-signature-hs512-0123456789");

        var token = CreateHelper(AccentedSecret).Create([new Claim("sub", "user-1")]);

        // A different configured secret, yet the same key — this is the characterized behaviour.
        (await CreateHelper(asciiFolded).Validate(token)).ShouldBeTrue();
    }

    /// <summary>
    /// Lifetime validation is driven by the *issuing* lifespan: <c>LifeSpan = 0</c> means "mint tokens that
    /// never expire", and it also switches off expiry checking on the validating side. The two are one knob.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(120, true)]
    public void Test_ValidateLifetime_Follows_LifeSpan(int lifeSpan, bool expected)
    {
        var options = ResolveJwtBearerOptions(o =>
        {
            o.Secret = AccentedSecret;
            o.LifeSpan = lifeSpan;
        });

        options.TokenValidationParameters.ValidateLifetime.ShouldBe(expected);
    }

    /// <summary>
    /// Zero clock skew, against the framework's 5-minute default: tokens expire exactly when they say they do.
    /// Deliberate, and the reason the expiry test in <see cref="JwtAuthenticationTests"/> can rely on a 2-second
    /// lifespan.
    /// </summary>
    [Fact]
    public void Test_ClockSkew_Is_Zero()
    {
        var options = ResolveJwtBearerOptions(o => o.Secret = AccentedSecret);

        options.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>
    /// Issuer and audience validation are each enabled only by configuring them. A setup carrying nothing but a
    /// <c>Secret</c> validates the signature and the lifetime and accepts any issuer and any audience — which is
    /// why the guides insist on setting <c>Audience</c> for a real deployment.
    /// </summary>
    [Fact]
    public void Test_Issuer_And_Audience_Validation_Are_Off_Until_Configured()
    {
        var bare = ResolveJwtBearerOptions(o => o.Secret = AccentedSecret);

        bare.TokenValidationParameters.ValidateIssuer.ShouldBeFalse();
        bare.TokenValidationParameters.ValidateAudience.ShouldBeFalse();

        var configured = ResolveJwtBearerOptions(o =>
        {
            o.Secret = AccentedSecret;
            o.Authority = "https://issuer.example";
            o.Audience = "spa";
        });

        configured.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        configured.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        configured.TokenValidationParameters.ValidIssuer.ShouldBe("https://issuer.example");
        configured.TokenValidationParameters.ValidAudiences.ShouldBe(["spa"]);
    }

    /// <summary>
    /// A single <c>Audience</c> is promoted to <c>ValidAudiences</c>, and an explicit <c>Audiences</c> list wins
    /// over it outright rather than being merged with it.
    /// </summary>
    [Fact]
    public void Test_Audiences_Collection_Replaces_Single_Audience()
    {
        var options = ResolveJwtBearerOptions(o =>
        {
            o.Secret = AccentedSecret;
            o.Audience = "ignored";
            o.Audiences = ["spa", "mobile"];
        });

        options.TokenValidationParameters.ValidAudiences.ShouldBe(["spa", "mobile"]);
    }

    /// <summary>A missing secret fails at registration, naming the property and the options type.</summary>
    [Fact]
    public void Test_Missing_Secret_Throws_At_Registration()
    {
        var exception = Should.Throw<NullReferenceException>(() =>
            new ServiceCollection().AddJwtAuthentication(_ => { }));

        exception.Message.ShouldContain(nameof(JwtTokenOptions.Secret));
        exception.Message.ShouldContain(typeof(JwtTokenOptions).FullName!);
    }

    /// <summary>
    /// The HS512 default needs 64 bytes. A shorter secret used to pass startup and throw <c>IDX10720</c> from the
    /// first login; it now fails at registration, and the message names the remedies.
    /// </summary>
    [Fact]
    public void Test_Secret_Too_Short_For_Default_Algorithm_Throws_At_Registration()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddJwtAuthentication(o => o.Secret = new string('k', 63)));

        exception.Message.ShouldContain("63 bytes");
        exception.Message.ShouldContain("HS512");
        exception.Message.ShouldContain("64");
    }

    /// <summary>
    /// The rule follows the configured algorithm rather than the strictest one: 32 bytes is short for HS512 but
    /// exactly right for HS256, and a consumer who names HS256 is not forced to a longer key.
    /// </summary>
    [Theory]
    [InlineData("HS256", 32)]
    [InlineData("HS384", 48)]
    [InlineData("HS512", 64)]
    public void Test_Minimum_Secret_Length_Follows_The_Configured_Algorithm(string algorithm, int minimumBytes)
    {
        Should.NotThrow(() => new ServiceCollection().AddJwtAuthentication(o =>
        {
            o.Secret = new string('k', minimumBytes);
            o.Algorithm = algorithm;
        }));

        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddJwtAuthentication(o =>
        {
            o.Secret = new string('k', minimumBytes - 1);
            o.Algorithm = algorithm;
        })).Message.ShouldContain(algorithm);
    }

    /// <summary>
    /// The XML-dsig URI spelling of an algorithm is the one <see cref="JwtTokenHelper"/> defaults to, so the rule
    /// has to recognise it — and report the JWA id, which is what the token header carries.
    /// </summary>
    [Fact]
    public void Test_Algorithm_Uri_Spelling_Is_Recognised_And_Reported_As_The_Jwa_Id()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new ServiceCollection().AddJwtAuthentication(o =>
            {
                o.Secret = new string('k', 16);
                o.Algorithm = SecurityAlgorithms.HmacSha256Signature;
            }));

        exception.Message.ShouldContain("HS256");
        exception.Message.ShouldNotContain("xmldsig");
    }

    /// <summary>
    /// An algorithm the rule does not know is left alone — rejecting it would break a working asymmetric or
    /// future-algorithm configuration on a guess.
    /// </summary>
    [Fact]
    public void Test_Unknown_Algorithm_Skips_The_Length_Check()
    {
        Should.NotThrow(() => new ServiceCollection().AddJwtAuthentication(o =>
        {
            o.Secret = "short";
            o.Algorithm = SecurityAlgorithms.RsaSha256;
        }));
    }

    /// <summary>
    /// The escape hatch for a validate-only scheme, where the length the local secret needs is whatever the
    /// external issuer signed with rather than anything <c>Algorithm</c> describes.
    /// </summary>
    [Fact]
    public void Test_Length_Check_Can_Be_Opted_Out_Of()
    {
        Should.NotThrow(() => new ServiceCollection().AddJwtAuthentication(o =>
        {
            o.Secret = "short";
            o.ValidateSecretLength = false;
        }));
    }

    /// <summary>
    /// Length is counted in the encoding the key is derived from. An accented secret is shorter in bytes than in
    /// characters under UTF-8 — but the key uses ASCII, where each accent still costs exactly one byte, so the
    /// count must agree with <see cref="Test_Secret_Is_Read_As_Ascii_So_Non_Ascii_Characters_Collapse"/>.
    /// </summary>
    [Fact]
    public void Test_Secret_Length_Is_Counted_In_The_Encoding_The_Key_Uses()
    {
        Encoding.ASCII.GetByteCount(AccentedSecret).ShouldBe(AccentedSecret.Length);

        Should.NotThrow(() => new ServiceCollection().AddJwtAuthentication(o => o.Secret = AccentedSecret));
    }

    /// <summary>
    /// The name and role claim types keep their JWT spellings. <c>role</c> in particular must not become
    /// <see cref="ClaimTypes.Role"/> here — the inbound claim-type map deliberately leaves it alone, so the two
    /// have to agree or <see cref="ClaimsPrincipal.IsInRole"/> resolves against a claim type nothing carries.
    /// </summary>
    [Fact]
    public void Test_Name_And_Role_Claim_Types_Keep_Their_Jwt_Spelling()
    {
        var options = ResolveJwtBearerOptions(o => o.Secret = AccentedSecret);

        options.TokenValidationParameters.NameClaimType.ShouldBe("name");
        options.TokenValidationParameters.RoleClaimType.ShouldBe("role");
    }

    /// <summary><c>ITokenHelper</c> is registered transient, and resolves to the JWT implementation.</summary>
    [Fact]
    public void Test_TokenHelper_Is_Registered_Transient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(o => o.Secret = AccentedSecret);

        var descriptor = services.Single(x => x.ServiceType == typeof(ITokenHelper));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ITokenHelper>().ShouldBeOfType<JwtTokenHelper>();
    }

    private static JwtTokenHelper CreateHelper(string secret) => new(new JwtTokenOptions
    {
        Secret = secret,
        Authority = "https://issuer.example",
        Audience = "spa"
    });

    /// <summary>The <see cref="JwtBearerOptions"/> the scheme was registered with, without standing up a host.</summary>
    private static JwtBearerOptions ResolveJwtBearerOptions(Action<JwtTokenOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(configure);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }
}
