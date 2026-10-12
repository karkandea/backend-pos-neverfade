using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Mfa;

public sealed record MfaStatus(bool Enabled, bool Pending, bool Configured);
public sealed record MfaEnrollment(string ManualKey, string OtpAuthUri, DateTime ExpiresAtUtc);
public sealed record MfaConfirmation(IReadOnlyList<string> RecoveryCodes);

/// <summary>Tenant-scoped MFA enrollment, login proof and one-time recovery code consumption.</summary>
public sealed class MfaService(
    AppDbContext db,
    CurrentUser currentUser,
    MfaSecretProtector protector,
    IServiceProvider services)
{
    public async Task<MfaStatus> StatusAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var credential = await db.Set<MfaCredential>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        return new MfaStatus(credential?.EnabledAtUtc.HasValue == true,
            credential is { EnabledAtUtc: null } &&
                credential.EnrollmentExpiresAtUtc > DateTime.UtcNow,
            protector.Configured);
    }

    public async Task<MfaEnrollment> EnrollAsync(string? password, CancellationToken ct)
    {
        RequireConfigured();
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        if (string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw Error(401, "MFA_PASSWORD_INVALID", "Password saat ini tidak sesuai.");

        var credential = await db.Set<MfaCredential>()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (credential?.EnabledAtUtc.HasValue == true)
            throw Error(409, "MFA_ALREADY_ENABLED", "Autentikator sudah aktif.");

        var secret = MfaTotp.GenerateSecret();
        var expires = DateTime.UtcNow.AddMinutes(10);
        if (credential is null)
        {
            credential = new MfaCredential { TenantId = tenantId, UserId = userId };
            db.Set<MfaCredential>().Add(credential);
        }
        credential.ProtectedSecret = protector.Protect(secret, userId);
        credential.EnrollmentExpiresAtUtc = expires;
        credential.LastUsedStep = -1;
        credential.FailedAttempts = 0;
        credential.LockedUntilUtc = null;
        credential.RecoveryCodeHashesJson = "[]";
        try
        {
            await SaveOrConflictAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw Error(409, "MFA_ENROLLMENT_CONFLICT",
                "Aktivasi autentikator sedang berlangsung. Muat ulang status.");
        }

        var manual = MfaTotp.EncodeBase32(secret);
        var label = Uri.EscapeDataString("Neverfade POS:" + user.Username);
        var issuer = Uri.EscapeDataString("Neverfade POS");
        var uri = $"otpauth://totp/{label}?secret={manual}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
        return new MfaEnrollment(manual, uri, expires);
    }

    public async Task<MfaConfirmation> ConfirmAsync(string? code, CancellationToken ct)
    {
        RequireConfigured();
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var credential = await db.Set<MfaCredential>()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (credential is null || credential.EnabledAtUtc.HasValue ||
            credential.EnrollmentExpiresAtUtc <= DateTime.UtcNow)
            throw Error(409, "MFA_ENROLLMENT_EXPIRED", "Mulai aktivasi autentikator kembali.");

        var now = DateTime.UtcNow;
        CheckLock(credential, now);
        var secret = protector.Unprotect(credential.ProtectedSecret, userId);
        var matched = MfaTotp.Match(secret, code, now, credential.LastUsedStep);
        if (!matched.HasValue)
        {
            await RecordFailedProofAsync(credential, ct);
            if (credential.LockedUntilUtc > DateTime.UtcNow)
                throw Error(429, "MFA_RATE_LIMIT",
                    "Terlalu banyak percobaan. Coba nanti.");
            throw Error(401, "MFA_INVALID", "Kode autentikator tidak sesuai.");
        }

        var recoveryCodes = Enumerable.Range(0, 8)
            .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(10))).ToArray();
        credential.EnabledAtUtc = now;
        credential.LastUsedStep = matched.Value;
        credential.FailedAttempts = 0;
        credential.LockedUntilUtc = null;
        credential.RecoveryCodeHashesJson = JsonSerializer.Serialize(
            recoveryCodes.Select(HashRecovery).ToArray());

        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        user.TokenVersion++;
        var sessions = await db.SharedPosSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var session in sessions) session.RevokedAtUtc = now;
        await SaveOrConflictAsync(ct);
        return new MfaConfirmation(recoveryCodes);
    }

    public async Task VerifyLoginAsync(
        Guid tenantId, Guid userId, string? totpCode, string? recoveryCode,
        CancellationToken ct)
    {
        // Login normally runs anonymously. Some API clients also send an existing
        // bearer token: reuse its tenant scope only when it matches the login target.
        using var trusted = BeginLoginScope(tenantId);
        var credential = await db.Set<MfaCredential>()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (credential?.EnabledAtUtc.HasValue != true)
            return;

        // Fail closed if an already-enrolled tenant cannot access its encryption key.
        RequireConfigured();
        if (string.IsNullOrWhiteSpace(totpCode) && string.IsNullOrWhiteSpace(recoveryCode))
            throw Error(401, "MFA_REQUIRED", "Masukkan kode autentikator atau pemulihan.");
        if (!string.IsNullOrWhiteSpace(totpCode) && !string.IsNullOrWhiteSpace(recoveryCode))
            throw Error(400, "MFA_INVALID", "Pilih satu metode autentikasi.");

        var now = DateTime.UtcNow;
        CheckLock(credential, now);
        var success = false;
        if (!string.IsNullOrWhiteSpace(totpCode))
        {
            var secret = protector.Unprotect(credential.ProtectedSecret, userId);
            var matched = MfaTotp.Match(secret, totpCode.Trim(), now, credential.LastUsedStep);
            if (matched.HasValue)
            {
                credential.LastUsedStep = matched.Value;
                success = true;
            }
        }
        else
        {
            var recovery = recoveryCode!.Trim().ToUpperInvariant();
            if (recovery.Length == 20 && recovery.All(Uri.IsHexDigit))
            {
                var hashes = JsonSerializer.Deserialize<List<string>>(
                    credential.RecoveryCodeHashesJson) ?? [];
                var hash = HashRecovery(recovery);
                var index = hashes.FindIndex(item =>
                    CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(item), Convert.FromHexString(hash)));
                if (index >= 0)
                {
                    hashes.RemoveAt(index);
                    credential.RecoveryCodeHashesJson = JsonSerializer.Serialize(hashes);
                    success = true;
                }
            }
        }

        if (!success)
        {
            await RecordFailedProofAsync(credential, ct);
            if (credential.LockedUntilUtc > DateTime.UtcNow)
                throw Error(429, "MFA_RATE_LIMIT",
                    "Terlalu banyak percobaan. Coba nanti.");
            throw Error(401, "MFA_INVALID",
                "Kode autentikator atau pemulihan tidak sesuai.");
        }
        credential.FailedAttempts = 0;
        credential.LockedUntilUtc = null;
        await SaveOrConflictAsync(ct);
    }

    private IDisposable? BeginLoginScope(Guid tenantId)
    {
        var context = services.GetRequiredService<ITenantExecutionContext>();
        if (context.Mode == TenantExecutionMode.None)
            return services.GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "MFA_LOGIN_VERIFY");
        if (context.Mode == TenantExecutionMode.AuthenticatedTenant &&
            context.TargetTenantId == tenantId)
            return null;
        throw Error(409, "MFA_LOGIN_TENANT_CONFLICT",
            "Akun yang sedang aktif berasal dari toko lain. Keluar sebelum login ulang.");
    }

    private void RequireConfigured()
    {
        if (!protector.Configured)
            throw Error(503, "MFA_UNAVAILABLE", "Autentikator belum dikonfigurasi.");
    }

    private static string HashRecovery(string code) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.ASCII.GetBytes(code.Trim().ToUpperInvariant())));

    private static void CheckLock(MfaCredential credential, DateTime now)
    {
        if (credential.LockedUntilUtc > now)
            throw Error(429, "MFA_RATE_LIMIT", "Terlalu banyak percobaan. Coba nanti.");
        if (credential.LockedUntilUtc.HasValue)
        {
            credential.LockedUntilUtc = null;
            credential.FailedAttempts = 0;
        }
    }

    private async Task RecordFailedProofAsync(MfaCredential credential, CancellationToken ct)
    {
        credential.FailedAttempts++;
        if (credential.FailedAttempts >= 5)
            credential.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
        await SaveOrConflictAsync(ct);
    }

    private async Task SaveOrConflictAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw Error(409, "MFA_CONCURRENT_CHANGE", "State autentikator berubah. Coba lagi.");
        }
    }

    private static TenantApiException Error(int status, string code, string message) =>
        new(status, code, message);
}
