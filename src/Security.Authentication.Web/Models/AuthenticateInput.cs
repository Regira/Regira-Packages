using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

/// <summary>The body of <c>POST auth?clientApp=…</c>. <c>clientApp</c>, the token audience, is a required query parameter, not a member of this body.</summary>
public record AuthenticateInput
{
    [Required]
    public string Username { get; set; } = null!;
    [Required]
    public string Password { get; set; } = null!;
}