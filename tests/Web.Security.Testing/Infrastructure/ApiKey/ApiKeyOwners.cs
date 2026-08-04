using Regira.Security.Authentication.ApiKey.Models;

namespace Web.Security.Testing.Infrastructure.ApiKey;

public static class ApiKeyOwners
{
    public static ApiKeyOwner Default =>
        new() { Key = "TEST", OwnerId = "TestOwnerId" };

    public static ApiKeyOwner WithAdminRole =>
        new() { Key = "ADMIN_TEST", OwnerId = "AdminOwnerId", Roles = ["admin"] };

    public static ApiKeyOwner WithClaim =>
        new() { Key = "CLAIM_TEST", OwnerId = "ClaimOwnerId", Claims = [new ApiKeyOwner.Claim("department", "engineering")] };

    public static ApiKeyOwner WithRolesAndClaims =>
        new() { Key = "FULL_TEST", OwnerId = "FullOwnerId", Roles = ["admin", "editor"], Claims = [new ApiKeyOwner.Claim("tenant", "acme")] };

    public static IList<ApiKeyOwner> Value => [Default, WithAdminRole, WithClaim, WithRolesAndClaims];
}