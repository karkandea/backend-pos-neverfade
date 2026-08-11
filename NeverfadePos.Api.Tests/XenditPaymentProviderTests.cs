using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NeverfadePos.Api.Payments.Xendit;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class XenditPaymentProviderTests
{
    [Fact]
    public async Task CreateQris_UsesPaymentsApiV3Contract()
    {
        var handler = new RecordingHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.xendit.co/")
        };
        var provider = new XenditPaymentProvider(
            client,
            Options.Create(new XenditOptions
            {
                SecretApiKey = "xnd_development_test_key"
            }));

        var result = await provider.CreateQrisAsync(
            "nf-test-reference",
            15000m,
            "NeverFade POS test");

        Assert.Equal(
            new Uri("https://api.xendit.co/v3/payment_requests"),
            handler.RequestUri);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("2024-11-11", handler.ApiVersion);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                "xnd_development_test_key:")),
            handler.BasicCredential);

        using var payload = JsonDocument.Parse(handler.Body!);
        var root = payload.RootElement;
        Assert.Equal("nf-test-reference", root.GetProperty("reference_id").GetString());
        Assert.Equal("PAY", root.GetProperty("type").GetString());
        Assert.Equal("ID", root.GetProperty("country").GetString());
        Assert.Equal("IDR", root.GetProperty("currency").GetString());
        Assert.Equal(15000m, root.GetProperty("request_amount").GetDecimal());
        Assert.Equal("QRIS", root.GetProperty("channel_code").GetString());
        Assert.Equal("pr-test", result.PaymentRequestId);
        Assert.Equal("000201010212TEST", result.QrString);
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
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """
                    {
                      "payment_request_id": "pr-test",
                      "reference_id": "nf-test-reference",
                      "request_amount": 15000,
                      "status": "REQUIRES_ACTION",
                      "actions": [
                        {
                          "type": "PRESENT_TO_CUSTOMER",
                          "descriptor": "QR_STRING",
                          "value": "000201010212TEST"
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
