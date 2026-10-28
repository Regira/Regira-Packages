using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Models;

namespace Web.Security.Testing.Infrastructure;

/// <summary>
/// A token the host issued, as it reads once its lifespan has run out: the same claims, issued and valid from ten
/// minutes back and expired a second ago, signed with the host's own <see cref="JwtTokenOptions"/>. The host keeps no
/// clock skew, so a test needs no wait for it.
/// </summary>
public static class ExpiredTokens
{
    public static string Of(string token, IServiceProvider hostServices)
    {
        var options = hostServices.GetRequiredService<JwtTokenOptions>();
        var payload = new JwtPayload();
        foreach (var (type, value) in new JwtSecurityTokenHandler().ReadJwtToken(token).Payload)
        {
            payload[type] = value;
        }
        var now = DateTimeOffset.UtcNow;
        payload[JwtRegisteredClaimNames.Iat] = now.AddMinutes(-10).ToUnixTimeSeconds();
        payload[JwtRegisteredClaimNames.Nbf] = now.AddMinutes(-10).ToUnixTimeSeconds();
        payload[JwtRegisteredClaimNames.Exp] = now.AddSeconds(-1).ToUnixTimeSeconds();

        // as JwtTokenHelper signs: the secret's ASCII bytes, HMAC-SHA512 unless the options name another algorithm
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(options.Secret));
        var credentials = new SigningCredentials(key, options.Algorithm ?? SecurityAlgorithms.HmacSha512Signature);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }
}
