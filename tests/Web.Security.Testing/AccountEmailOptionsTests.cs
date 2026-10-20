using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Web.Models;
using Shouldly;
using Xunit;

namespace Web.Security.Testing;

// The registration the guides recommend: bound from configuration and validated on start, so a host without an origin
// stops at startup instead of failing its first password recovery. The host runs IStartupValidator when it starts.
public class AccountEmailOptionsTests
{
    [Fact]
    public void ValidatedOnStart_WithoutAnOrigin_StopsTheHost()
    {
        using var provider = Register(new() { ["Authentication:AccountEmails:SiteName"] = "Example" });

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate())
            .Message.ShouldContain("lists no origin");
    }

    [Fact]
    public void ValidatedOnStart_WithAnOrigin_BindsIt()
    {
        using var provider = Register(new()
        {
            ["Authentication:AccountEmails:SiteName"] = "Example",
            ["Authentication:AccountEmails:AllowedOrigins:0"] = "https://app.example.com"
        });

        provider.GetRequiredService<IStartupValidator>().Validate();
        var options = provider.GetRequiredService<IOptions<AccountEmailOptions>>().Value;
        options.AllowedOrigins.Single().ShouldBe("https://app.example.com");
        options.SiteName.ShouldBe("Example");
    }

    // as in security.instructions → Mailed links: allowed origins
    private static ServiceProvider Register(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddOptions<AccountEmailOptions>()
            .Bind(configuration.GetSection("Authentication:AccountEmails"))
            .Validate(o => o.AllowedOrigins.Count > 0, "Authentication:AccountEmails:AllowedOrigins lists no origin.")
            .ValidateOnStart();
        return services.BuildServiceProvider();
    }
}
