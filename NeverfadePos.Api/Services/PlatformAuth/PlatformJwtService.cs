using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.PlatformAuth;

public sealed class PlatformJwtService(
    IConfiguration configuration)
    : IPlatformJwtService
{
    public string GenerateToken(PlatformUser user)
    {
        var key = Require("PlatformJwt:Key");
        var issuer = Require("PlatformJwt:Issuer");
        var audience = Require("PlatformJwt:Audience");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),
            new(
                PlatformAuthConstants.ScopeClaim,
                PlatformAuthConstants.PlatformScope),
            new(
                ClaimTypes.Role,
                PlatformAuthConstants.SuperAdminRole),
            new("username", user.Username),
            new("nama", user.Nama)
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

    private string Require(string key)
    {
        return configuration[key]
            ?? throw new InvalidOperationException(
                $"{key} missing.");
    }
}
