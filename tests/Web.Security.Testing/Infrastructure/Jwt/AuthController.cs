using Duende.IdentityModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regira.Security.Authentication.Jwt.Abstraction;

namespace Web.Security.Testing.Infrastructure.Jwt;

[ApiController]
[Route("auth")]
public class AuthController(ITokenHelper tokenHelper) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public IActionResult Login(AuthInput input)
    {
        var user = JwtUsers.Value.FirstOrDefault(x => x.Name == input.Username);
        // for testing purposes, accept any password except when empty
        if (user == null || string.IsNullOrWhiteSpace(input.Password))
        {
            return StatusCode(StatusCodes.Status401Unauthorized);
        }
        // Emitting the plain "role" claim is what the documentation prescribes: JwtTokenOptions.RoleClaimType
        // defaults to it, so a token carrying ClaimTypes.Role instead would not resolve.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId!),
            new(ClaimTypes.Name, user.Name!),
            new(ClaimTypes.Email, user.Email!)
        };
        if (user.Role != null)
        {
            claims.Add(new Claim(JwtClaimTypes.Role, user.Role));
        }
        var token = tokenHelper.Create(claims);

        return Ok(new TokenResult
        {
            IsAuthenticated = true,
            Token = token
        });
    }
}