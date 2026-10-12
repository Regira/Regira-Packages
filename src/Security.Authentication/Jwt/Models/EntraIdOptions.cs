using Microsoft.AspNetCore.Authentication.JwtBearer;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Jwt.Models;

/// <summary>
/// Microsoft Entra ID, expressed as the few values an app registration gives you. Expands to
/// <see cref="BearerValidationOptions"/> with the authority, audiences, issuers and claim types Entra actually uses.
/// </summary>
public class EntraIdOptions : EntraIdOptionsBase
{
    /// <summary>
    /// Overrides the derived audiences. Left null, both <c>api://{ClientId}</c> and <c>{ClientId}</c> are accepted —
    /// Entra issues one or the other depending on how the client requested the scope, and pinning a single form
    /// rejects half of otherwise valid callers.
    /// </summary>
    public ICollection<string>? Audiences { get; set; }

    public string AuthenticationScheme { get; set; } = JwtBearerDefaults.AuthenticationScheme;

    public bool SaveToken { get; set; }

    /// <summary>Source claim types folded into the canonical set. Defaults already cover Entra's spellings.</summary>
    public ClaimNormalizationOptions Claims { get; } = new();

    public Action<JwtBearerOptions>? Configure { get; set; }
}
