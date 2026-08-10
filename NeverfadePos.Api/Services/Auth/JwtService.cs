using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Auth;

public sealed class JwtService(
    IConfiguration configuration)
    : IJwtService
{
    public string GenerateToken(User user)
    {
        var key =
            configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key missing.");

        var issuer =
            configuration["Jwt:Issuer"] ?? "NeverfadePos";

        var audience =
            configuration["Jwt:Audience"] ?? "NeverfadePos";

        var credentials =
            new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("scope", "tenant"),
            new("tenant_id", user.TenantId.ToString()),
            new("username", user.Username),
            new("nama", user.Nama),
            new(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
