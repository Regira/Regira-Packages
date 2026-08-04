using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Models;

namespace Regira.Security.Authentication.Jwt.Services;

public class JwtTokenHelper(JwtTokenOptions options) : ITokenHelper
{
    private readonly string _secret = options.Secret;
    private readonly string? _algorithm = options.Algorithm ?? SecurityAlgorithms.HmacSha512Signature;
    private readonly string? _authority = options.Authority;
    private readonly string? _defaultAudience = options.Audience;
    private readonly int _defaultLifeSpan = options.LifeSpan;
    private readonly bool _includeIssuedDate = options.IncludeIssuedDate;
    private readonly ICollection<string>? _validAudiences = options.Audiences;


    public string Create(IEnumerable<Claim> claims, string? audience = null, int? lifeSpan = null)
    {
        var secretKey = Encoding.ASCII.GetBytes(_secret);
        var symmetricKey = new SymmetricSecurityKey(secretKey);

        lifeSpan ??= _defaultLifeSpan;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _authority,
            Audience = audience ?? _defaultAudience,
            Expires = lifeSpan.Value > 0 ? DateTime.UtcNow.AddSeconds(lifeSpan.Value) : null,
            SigningCredentials = new SigningCredentials(symmetricKey, _algorithm),
            IssuedAt = _includeIssuedDate ? DateTime.UtcNow : null
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    public async Task<bool> Validate(string token)
    {
        var secretKey = Encoding.ASCII.GetBytes(_secret);
        var symmetricKey = new SymmetricSecurityKey(secretKey);

        var tokenHandler = new JsonWebTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            IssuerSigningKey = symmetricKey,
            ValidIssuer = _authority,
            ValidAudiences = _validAudiences ?? (_defaultAudience != null ? new[] { _defaultAudience } : null)
        };
        var validateResult = await tokenHandler.ValidateTokenAsync(token, validationParams);

        return validateResult.IsValid;
    }
}