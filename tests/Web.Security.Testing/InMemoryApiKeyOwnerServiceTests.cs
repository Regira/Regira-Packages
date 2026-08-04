using Regira.Security.Authentication.ApiKey.Services;
using Shouldly;
using Web.Security.Testing.Infrastructure.ApiKey;
using Xunit;

namespace Web.Security.Testing;

public class InMemoryApiKeyOwnerServiceTests
{
    private readonly InMemoryApiKeyOwnerService _service = new(ApiKeyOwners.Value);

    [Fact]
    public async Task FindByKey_WithValidKey_ReturnsOwner()
    {
        var owner = await _service.FindByKey(ApiKeyOwners.Default.Key);

        owner.ShouldNotBeNull();
        owner.OwnerId.ShouldBe(ApiKeyOwners.Default.OwnerId);
    }

    [Fact]
    public async Task FindByKey_WithUnknownKey_ReturnsNull()
    {
        var owner = await _service.FindByKey("UNKNOWN_KEY");

        owner.ShouldBeNull();
    }

    [Fact]
    public async Task FindByOwner_WithValidId_ReturnsOwner()
    {
        var owner = await _service.FindByOwner(ApiKeyOwners.Default.OwnerId);

        owner.ShouldNotBeNull();
        owner.Key.ShouldBe(ApiKeyOwners.Default.Key);
    }

    [Fact]
    public async Task FindByOwner_IsCaseInsensitive()
    {
        var owner = await _service.FindByOwner(ApiKeyOwners.Default.OwnerId.ToLower());

        owner.ShouldNotBeNull();
        owner.OwnerId.ShouldBe(ApiKeyOwners.Default.OwnerId);
    }

    [Fact]
    public async Task FindByOwner_WithUnknownId_ReturnsNull()
    {
        var owner = await _service.FindByOwner("nonexistent");

        owner.ShouldBeNull();
    }

    [Fact]
    public async Task Validate_WithCorrectIdAndKey_ReturnsTrue()
    {
        var result = await _service.Validate(ApiKeyOwners.Default.OwnerId, ApiKeyOwners.Default.Key);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_WithWrongKey_ReturnsFalse()
    {
        var result = await _service.Validate(ApiKeyOwners.Default.OwnerId, "WRONG_KEY");

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_WithUnknownOwner_ReturnsFalse()
    {
        var result = await _service.Validate("unknown", ApiKeyOwners.Default.Key);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task FindByKey_OwnerWithRole_HasRoles()
    {
        var owner = await _service.FindByKey(ApiKeyOwners.WithAdminRole.Key);

        owner.ShouldNotBeNull();
        owner.Roles.ShouldContain("admin");
    }

    [Fact]
    public async Task FindByKey_OwnerWithClaim_HasClaims()
    {
        var owner = await _service.FindByKey(ApiKeyOwners.WithClaim.Key);

        owner.ShouldNotBeNull();
        owner.Claims.ShouldContain(c => c.Type == "department" && c.Value == "engineering");
    }

    [Fact]
    public async Task FindByKey_OwnerWithRolesAndClaims_HasAll()
    {
        var owner = await _service.FindByKey(ApiKeyOwners.WithRolesAndClaims.Key);

        owner.ShouldNotBeNull();
        owner.Roles.ShouldContain("admin");
        owner.Roles.ShouldContain("editor");
        owner.Claims.ShouldContain(c => c.Type == "tenant" && c.Value == "acme");
    }
}
