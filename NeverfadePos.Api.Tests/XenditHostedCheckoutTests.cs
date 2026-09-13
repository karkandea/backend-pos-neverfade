using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeverfadePos.Api.Payments;
using NeverfadePos.Api.Payments.Xendit;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class XenditHostedCheckoutTests
{
    [Fact]
    public void Capabilities_HideHostedCheckout_WhenReturnUrlMissing()
    {
        var tenantId = Guid.NewGuid();
        var gate = CreateGate(tenantId, string.Empty);

        var capabilities = gate.GetCapabilities(tenantId);

        Assert.True(capabilities.QrisEnabled);
        Assert.False(capabilities.HostedCheckoutEnabled);
    }

    [Fact]
    public void Capabilities_EnableHostedCheckout_WhenHttpsReturnUrlConfigured()
    {
        var tenantId = Guid.NewGuid();
        var gate = CreateGate(tenantId, "https://neverfade.example/kasir");

        var capabilities = gate.GetCapabilities(tenantId);

        Assert.True(capabilities.QrisEnabled);
        Assert.True(capabilities.HostedCheckoutEnabled);
    }

    [Fact]
    public async Task CreateHostedSession_UsesPaymentSessionContract()
    {
        var handler = new HostedSessionHandler();
        var provider = new XenditPaymentProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.xendit.co/") },
            Options.Create(new XenditOptions
            {
                SecretApiKey = "xnd_development_test_key",
                CheckoutReturnUrl = "https://neverfade.example/kasir"
            }));

        var expiresAt = new DateTime(2026, 9, 13, 11, 30, 0, DateTimeKind.Utc);
        var result = await provider.CreateHostedSessionAsync(
            "nf-hosted-test",
            25000m,
            "NeverFade POS hosted test",
            expiresAt);

        Assert.Equal(new Uri("https://api.xendit.co/sessions"), handler.RequestUri);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.False(handler.HasApiVersion);

        using var payload = JsonDocument.Parse(handler.Body!);
        var root = payload.RootElement;
        Assert.Equal("nf-hosted-test", root.GetProperty("reference_id").GetString());
        Assert.Equal("PAY", root.GetProperty("session_type").GetString());
        Assert.Equal("PAYMENT_LINK", root.GetProperty("mode").GetString());
        Assert.Equal("DISABLED", root.GetProperty("allow_save_payment_method").GetString());
        Assert.Equal("IDR", root.GetProperty("currency").GetString());
        Assert.Equal("ID", root.GetProperty("country").GetString());
        Assert.Equal(25000m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("https://neverfade.example/kasir", root.GetProperty("success_return_url").GetString());
        Assert.Equal("https://neverfade.example/kasir", root.GetProperty("cancel_return_url").GetString());
        Assert.Equal("ps-hosted-test", result.SessionId);
        Assert.Equal("https://checkout.xendit.test/session", result.PaymentLinkUrl);
    }

    private static PaymentModeGate CreateGate(Guid tenantId, string returnUrl) =>
        new(
            Options.Create(new PaymentModeOptions
            {
                Mode = "Sandbox",
                SandboxAllowedTenantIds = tenantId.ToString()
            }),
            Options.Create(new XenditOptions
            {
                SecretApiKey = "xnd_development_test_key",
                WebhookCallbackToken = "test-token",
                CheckoutReturnUrl = returnUrl
            }));

    private sealed class HostedSessionHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public bool HasApiVersion { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            HasApiVersion = request.Headers.Contains("api-version");
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """
                    {
                      "payment_session_id": "ps-hosted-test",
                      "reference_id": "nf-hosted-test",
                      "amount": 25000,
                      "status": "ACTIVE",
                      "payment_link_url": "https://checkout.xendit.test/session",
                      "expires_at": "2026-09-13T11:30:00Z",
                      "payment_request_id": null,
                      "payment_id": null
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
