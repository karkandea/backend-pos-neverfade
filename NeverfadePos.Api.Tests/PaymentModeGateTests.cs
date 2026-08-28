using Microsoft.Extensions.Options;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Payments;
using NeverfadePos.Api.Payments.Xendit;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class PaymentModeGateTests
{
    [Fact]
    public void Disabled_IsSafeDefaultAndNeedsNoProviderSecret()
    {
        var tenantId = Guid.NewGuid();
        var gate = CreateGate(new PaymentModeOptions());

        var capabilities = gate.GetCapabilities(tenantId);
        var exception = Assert.Throws<PaymentApiException>(
            () => gate.EnsureQrisAllowed(tenantId));

        Assert.False(capabilities.QrisEnabled);
        Assert.Equal("disabled", capabilities.Mode);
        Assert.False(capabilities.IsSandbox);
        Assert.Equal("PAYMENT_QRIS_DISABLED", exception.Code);
    }

    [Fact]
    public void Sandbox_AllowsOnlyExplicitTenantId()
    {
        var allowedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var gate = CreateGate(
            new PaymentModeOptions
            {
                Mode = "Sandbox",
                SandboxAllowedTenantIds = allowedTenantId.ToString()
            },
            "xnd_development_test_key");

        gate.EnsureQrisAllowed(allowedTenantId);
        var allowed = gate.GetCapabilities(allowedTenantId);
        var blocked = gate.GetCapabilities(otherTenantId);
        var exception = Assert.Throws<PaymentApiException>(
            () => gate.EnsureQrisAllowed(otherTenantId));

        Assert.True(allowed.QrisEnabled);
        Assert.True(allowed.IsSandbox);
        Assert.Equal("sandbox", allowed.Mode);
        Assert.False(blocked.QrisEnabled);
        Assert.True(blocked.IsSandbox);
        Assert.Equal(
            "PAYMENT_SANDBOX_TENANT_NOT_ALLOWED",
            exception.Code);
    }

    [Fact]
    public void Sandbox_RejectsMissingOrMalformedAllowlist()
    {
        Assert.Throws<InvalidOperationException>(() => CreateGate(
            new PaymentModeOptions { Mode = "Sandbox" },
            "xnd_development_test_key"));

        Assert.Throws<InvalidOperationException>(() => CreateGate(
            new PaymentModeOptions
            {
                Mode = "Sandbox",
                SandboxAllowedTenantIds = "not-a-guid"
            },
            "xnd_development_test_key"));
    }

    [Fact]
    public void Live_RequiresExplicitFlagAllowlistAndProductionKey()
    {
        var allowedTenantId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => CreateGate(
            new PaymentModeOptions
            {
                Mode = "Live",
                LiveAllowedTenantIds = allowedTenantId.ToString()
            },
            "xnd_production_test_key"));

        Assert.Throws<InvalidOperationException>(() => CreateGate(
            new PaymentModeOptions
            {
                Mode = "Live",
                LiveEnabled = true
            },
            "xnd_production_test_key"));

        Assert.Throws<InvalidOperationException>(() => CreateGate(
            new PaymentModeOptions
            {
                Mode = "Live",
                LiveEnabled = true,
                LiveAllowedTenantIds = allowedTenantId.ToString()
            },
            "xnd_development_test_key"));
    }

    [Fact]
    public void Live_AllowsOnlyExplicitTenantId()
    {
        var allowedTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var gate = CreateGate(
            new PaymentModeOptions
            {
                Mode = "Live",
                LiveEnabled = true,
                LiveAllowedTenantIds = allowedTenantId.ToString()
            },
            "xnd_production_test_key");

        gate.EnsureQrisAllowed(allowedTenantId);
        var allowed = gate.GetCapabilities(allowedTenantId);
        var blocked = gate.GetCapabilities(otherTenantId);
        var exception = Assert.Throws<PaymentApiException>(
            () => gate.EnsureQrisAllowed(otherTenantId));

        Assert.True(allowed.QrisEnabled);
        Assert.False(allowed.IsSandbox);
        Assert.Equal("live", allowed.Mode);
        Assert.False(blocked.QrisEnabled);
        Assert.False(blocked.IsSandbox);
        Assert.Equal(
            "PAYMENT_LIVE_TENANT_NOT_ALLOWED",
            exception.Code);
    }

    private static PaymentModeGate CreateGate(
        PaymentModeOptions paymentOptions,
        string secretKey = "") =>
        new(
            Options.Create(paymentOptions),
            Options.Create(new XenditOptions
            {
                SecretApiKey = secretKey,
                WebhookCallbackToken = "callback-token"
            }));
}
