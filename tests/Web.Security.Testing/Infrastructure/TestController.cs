using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regira.Security.Authentication.Jwt.Extensions;
using System.Security.Claims;

namespace Web.Security.Testing.Infrastructure;

[ApiController]
[Route("")]
public class TestController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Index()
    {
        return Ok("welcome");
    }

    [HttpGet("protected")]
    public IActionResult Protected()
    {
        return Ok("You're safe");
    }

    [Authorize(Roles = "admin")]
    [HttpGet("protected/admin")]
    public IActionResult AdminOnly()
    {
        return Ok("admin area");
    }

    [HttpGet("protected/me")]
    public IActionResult Me()
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(ownerId);
    }

    /// <summary>
    /// Reports the claims as the API sees them <em>after</em> validation, which is the only place the inbound
    /// claim-type map is observable — a token can carry a plain "role" claim that the principal no longer has.
    /// </summary>
    [HttpGet("protected/claims")]
    public IActionResult Claims()
    {
        return Ok(new ClaimsReport
        {
            // what a hand-written row-security check reads
            Role = User.FindFirstValue("role"),
            MappedRole = User.FindFirstValue(ClaimTypes.Role),
            // what [Authorize(Roles = ...)] resolves
            IsInAdminRole = User.IsInRole("admin"),
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Name = User.Identity?.Name,
            // the ClaimsPrincipal helpers the guides tell consumers to use
            FoundUserId = User.FindUserId(),
            FoundUserName = User.FindUserName(),
            FoundEmail = User.FindEmail()
        });
    }
}

public class ClaimsReport
{
    public string? Role { get; set; }
    public string? MappedRole { get; set; }
    public bool IsInAdminRole { get; set; }
    public string? UserId { get; set; }
    public string? Name { get; set; }
    public string? FoundUserId { get; set; }
    public string? FoundUserName { get; set; }
    public string? FoundEmail { get; set; }
}