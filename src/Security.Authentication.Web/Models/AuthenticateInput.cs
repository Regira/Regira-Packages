using Regira.Security.Authentication.Web.Constants;
using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

/// <summary>
/// The body of <c>POST auth?clientApp=…</c>. <c>clientApp</c>, the token audience, is a required query parameter, not a
/// member of this body; where the JWT scheme configures audiences it is one of them, or the request answers <c>400</c>.
/// </summary>
public record AuthenticateInput
{
    [Required, MaxLength(AccountInputLimits.UserName)]
    public string Username { get; set; } = null!;
    [Required, MaxLength(AccountInputLimits.Password)]
    public string Password { get; set; } = null!;
}