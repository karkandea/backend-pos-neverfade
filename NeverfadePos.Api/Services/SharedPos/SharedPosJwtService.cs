using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.SharedPos;

public sealed record SharedPosJwtResult(
    string Token,
    DateTime ExpiresAtUtc,
    DateTime? ReauthUntilUtc);

public interface ISharedPosJwtService
{
    SharedPosJwtResult Generate(User user, Guid karyawanId, Guid sharedSessionId);
}

internal sealed class SharedPosJwtService(IConfiguration configuration)
    : ISharedPosJwtService
{
    public SharedPosJwtResult Generate(User user, Guid karyawanId, Guid sharedSessionId)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key missing.");
        var issuer = configuration["Jwt:Issuer"] ?? "NeverfadePos";
        var audience = configuration["Jwt:Audience"] ?? "NeverfadePos";
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(30);
        var requiresRecentReauth = user.Role is "owner" or "admin";
        DateTime? reauthUntil = requiresRecentReauth ? now.AddMinutes(5) : null;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("scope", "tenant"),
            new("tenant_id", user.TenantId.ToString()),
            new("username", user.Username),
            new("nama", user.Nama),
            new(ClaimTypes.Role, user.Role),
            new("session_kind", "shared_pos"),
            new("employee_id", karyawanId.ToString()),
            new("shared_session_id", sharedSessionId.ToString()),
            new("auth_time", new DateTimeOffset(now).ToUnixTimeSeconds().ToString())
        };

        if (reauthUntil.HasValue)
        {
            claims.Add(new Claim(
                "shared_reauth_until",
                new DateTimeOffset(reauthUntil.Value).ToUnixTimeSeconds().ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new SharedPosJwtResult(
            new JwtSecurityTokenHandler().WriteToken(jwt),
            expires,
            reauthUntil);
    }
}
