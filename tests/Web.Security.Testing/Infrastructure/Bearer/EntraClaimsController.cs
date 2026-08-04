using Microsoft.AspNetCore.Mvc;
using Regira.Security.Authentication.Jwt.Extensions;
using System.Security.Claims;

namespace Web.Security.Testing.Infrastructure.Bearer;

[ApiController]
[Route("entra")]
public class EntraClaimsController : ControllerBase
{
    /// <summary>Reports what the Entra principal looks like after validation and normalization.</summary>
    [HttpGet("claims")]
    public IActionResult Claims()
    {
        return Ok(new EntraClaimsReport
        {
            // Entra's own spellings must survive
            ObjectId = User.FindFirstValue("oid"),
            Roles = User.FindFirstValue("roles"),
            // the canonical copies normalization added
            Subject = User.FindFirstValue("sub"),
            Role = User.FindFirstValue("role"),
            IsInAdminRole = User.IsInRole(FakeAuthority.AdminRole),
            FoundUserName = User.FindUserName(),
            AllRoles = User.FindRoles().ToArray(),
            HasReadScope = User.HasScope("api.read"),
            HasWriteScope = User.HasScope("api.write")
        });
    }
}

public class EntraClaimsReport
{
    public string? ObjectId { get; set; }
    public string? Roles { get; set; }
    public string? Subject { get; set; }
    public string? Role { get; set; }
    public bool IsInAdminRole { get; set; }
    public string? FoundUserName { get; set; }
    public string[] AllRoles { get; set; } = [];
    public bool HasReadScope { get; set; }
    public bool HasWriteScope { get; set; }
}
