using Microsoft.Extensions.Configuration;
using Regira.Security.Authentication.ApiKey.Extensions;
using Shouldly;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// What <see cref="ConfigurationExtensions.ToApiKeyOwners"/> says when an entry is incomplete: a key left out of
/// <c>appsettings.json</c> and never set, or an object keyed by owner instead of an array.
/// </summary>
public class ApiKeyConfigurationTests
{
    private static IConfigurationSection ApiKeys(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("Authentication:ApiKeys");

    [Fact]
    public void A_Key_Never_Set_Names_The_Path_To_Set()
    {
        var section = ApiKeys(new() { ["Authentication:ApiKeys:0:OwnerId"] = "ci-pipeline" });

        var ex = Should.Throw<InvalidOperationException>(() => section.ToApiKeyOwners());

        ex.Message.ShouldContain("\"Authentication:ApiKeys:0:Key\"");
        ex.Message.ShouldContain("user-secrets");
        ex.Message.ShouldContain("Authentication__ApiKeys__0__Key");
        ex.Message.ShouldNotContain("keyed by owner");
    }

    [Fact]
    public void An_Object_Keyed_By_Owner_Names_The_Expected_Shape()
    {
        var section = ApiKeys(new() { ["Authentication:ApiKeys:client-a:Key"] = "key-abc" });

        var ex = Should.Throw<InvalidOperationException>(() => section.ToApiKeyOwners());

        ex.Message.ShouldContain("OwnerId is missing");
        ex.Message.ShouldContain("not an object keyed by owner");
    }

    [Fact]
    public void A_Complete_Entry_Is_Read()
    {
        var section = ApiKeys(new()
        {
            ["Authentication:ApiKeys:0:OwnerId"] = "ci-pipeline",
            ["Authentication:ApiKeys:0:Key"] = "key-abc",
            ["Authentication:ApiKeys:0:Roles:0"] = "read"
        });

        var owner = section.ToApiKeyOwners().ShouldHaveSingleItem();

        owner.OwnerId.ShouldBe("ci-pipeline");
        owner.Key.ShouldBe("key-abc");
        owner.Roles.ShouldBe(["read"]);
    }
}
