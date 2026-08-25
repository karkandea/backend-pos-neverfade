using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeverfadePos.Api.Payments.Xendit;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class XenditSandboxSimulatorTests
{
    [Fact]
    public async Task SimulatePayment_UsesSandboxSimulationContract()
    {
        var handler = new RecordingHandler();
        var simulator = new XenditSandboxSimulator(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.xendit.co/")
            },
            Options.Create(new XenditOptions
            {
                SecretApiKey = "xnd_development_test_key"
            }));

        await simulator.SimulatePaymentAsync(
            "pr-test-payment",
            11100m);

        Assert.Equal(
            new Uri("https://api.xendit.co/v3/payment_requests/pr-test-payment/simulate"),
            handler.RequestUri);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("2024-11-11", handler.ApiVersion);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                "xnd_development_test_key:")),
            handler.BasicCredential);

        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal(
            11100m,
            payload.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task SimulatePayment_RejectsProductionKeyBeforeRequest()
    {
        var handler = new RecordingHandler();
        var simulator = new XenditSandboxSimulator(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.xendit.co/")
            },
            Options.Create(new XenditOptions
            {
                SecretApiKey = "xnd_production_test_key"
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            simulator.SimulatePaymentAsync(
                "pr-test-payment",
                11100m));

        Assert.Null(handler.RequestUri);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? BasicCredential { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            ApiVersion = request.Headers.GetValues("api-version").Single();
            BasicCredential = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """
                    {
                      "status": "PENDING",
                      "message": "A simulated payment is being processed."
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
