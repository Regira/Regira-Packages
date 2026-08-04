namespace Regira.Security.Authentication.Jwt.Models;

public static class EntraIdDefaults
{
    public const string Instance = "https://login.microsoftonline.com";

    /// <summary>Any work or school account, no personal Microsoft accounts.</summary>
    public const string OrganizationsTenant = "organizations";

    /// <summary>Any work or school account <em>and</em> personal Microsoft accounts.</summary>
    public const string CommonTenant = "common";

    /// <summary>The v1 issuer host. A registration that has not opted into v2 tokens issues under this.</summary>
    public const string V1IssuerHost = "https://sts.windows.net";

    /// <summary>Entra's spelling for app roles — <c>role</c> singular matches nothing on an Entra token.</summary>
    public const string RoleClaimType = "roles";

    /// <summary>The tenant id claim, used to validate a multi-tenant issuer.</summary>
    public const string TenantIdClaimType = "tid";
}
