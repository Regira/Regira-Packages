using System.Text.Json.Serialization;

namespace Regira.Security.Authentication.Web.Models;

public record AuthenticateResponseDto
{
    public bool IsAuthenticated { get; set; }
    public string? Token { get; set; }

    /// <summary>
    /// Set only when an <c>IRefreshTokenService</c> is registered, and omitted from the JSON otherwise — so a host that
    /// has not opted into refresh tokens serves the same response body it always did.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; set; }

    /// <summary>When <see cref="Token"/> expires, so a client can refresh ahead of it rather than on a 401.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ExpiresAt { get; set; }

    public bool? IsLockedOut { get; set; }
    public DateTime? LockedOutEnd { get; set; }
}