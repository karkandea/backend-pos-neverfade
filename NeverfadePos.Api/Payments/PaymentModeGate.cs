using Microsoft.Extensions.Options;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.Payments.Xendit;

namespace NeverfadePos.Api.Payments;

public sealed class PaymentModeGate : IPaymentModeGate
{
    private readonly PaymentMode _mode;
    private readonly HashSet<Guid> _sandboxAllowedTenantIds;

    public PaymentModeGate(
        IOptions<PaymentModeOptions> paymentOptions,
        IOptions<XenditOptions> xenditOptions)
    {
        var options = paymentOptions.Value;
        _mode = ParseMode(options.Mode);
        _sandboxAllowedTenantIds = ParseTenantIds(
            options.SandboxAllowedTenantIds);

        ValidateConfiguration(options, xenditOptions.Value);
    }

    public PaymentCapabilitiesDto GetCapabilities(Guid tenantId)
    {
        var sandbox = _mode == PaymentMode.Sandbox;
        var enabled = _mode switch
        {
            PaymentMode.Live => true,
            PaymentMode.Sandbox =>
                _sandboxAllowedTenantIds.Contains(tenantId),
            _ => false
        };

        return new PaymentCapabilitiesDto
        {
            QrisEnabled = enabled,
            Mode = _mode.ToString().ToLowerInvariant(),
            IsSandbox = sandbox
        };
    }

    public void EnsureQrisAllowed(Guid tenantId)
    {
        if (_mode == PaymentMode.Disabled)
        {
            throw new PaymentApiException(
                StatusCodes.Status503ServiceUnavailable,
                "PAYMENT_QRIS_DISABLED",
                "Pembayaran QRIS sedang tidak tersedia.");
        }

        if (_mode == PaymentMode.Sandbox &&
            !_sandboxAllowedTenantIds.Contains(tenantId))
        {
            throw new PaymentApiException(
                StatusCodes.Status403Forbidden,
                "PAYMENT_SANDBOX_TENANT_NOT_ALLOWED",
                "QRIS Sandbox tidak tersedia untuk tenant ini.");
        }
    }

    private void ValidateConfiguration(
        PaymentModeOptions options,
        XenditOptions xendit)
    {
        if (_mode == PaymentMode.Disabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(xendit.WebhookCallbackToken))
        {
            throw new InvalidOperationException(
                "Xendit:WebhookCallbackToken is required when payments are enabled.");
        }

        if (_mode == PaymentMode.Sandbox)
        {
            if (_sandboxAllowedTenantIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "Payments:SandboxAllowedTenantIds must contain at least one tenant ID in Sandbox mode.");
            }

            if (!xendit.SecretApiKey.StartsWith(
                    "xnd_development_",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Sandbox mode requires an Xendit development secret key.");
            }

            return;
        }

        if (!options.LiveEnabled)
        {
            throw new InvalidOperationException(
                "Payments:LiveEnabled must be explicitly true in Live mode.");
        }

        if (!xendit.SecretApiKey.StartsWith(
                "xnd_production_",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Live mode requires an Xendit production secret key.");
        }
    }

    private static PaymentMode ParseMode(string? value)
    {
        if (Enum.TryParse<PaymentMode>(
                value?.Trim(),
                ignoreCase: true,
                out var mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            "Payments:Mode must be Disabled, Sandbox, or Live.");
    }

    private static HashSet<Guid> ParseTenantIds(string? value)
    {
        var tenantIds = new HashSet<Guid>();

        foreach (var item in (value ?? string.Empty).Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(item, out var tenantId) ||
                tenantId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Payments:SandboxAllowedTenantIds contains an invalid tenant ID.");
            }

            tenantIds.Add(tenantId);
        }

        return tenantIds;
    }

    private enum PaymentMode
    {
        Disabled,
        Sandbox,
        Live
    }
}
