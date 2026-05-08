using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceBot.Application.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinanceBot.Infrastructure.Authentication;

public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly IOptions<JwtOptions> _jwtOptions;

    public JwtAccessTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public AccessTokenResult Generate(Guid usuarioId, string email)
    {
        var options = _jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException("Jwt:Key deve ser configurado para emitir tokens.");
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(options.TokenExpirationMinutes <= 0 ? 60 : options.TokenExpirationMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Email, email)
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
