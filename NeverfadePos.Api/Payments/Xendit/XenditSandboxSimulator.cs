using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace NeverfadePos.Api.Payments.Xendit;

public sealed class XenditSandboxSimulator(
    HttpClient httpClient,
    IOptions<XenditOptions> options)
    : IXenditSandboxSimulator
{
    private const string ApiVersion = "2024-11-11";

    public async Task SimulatePaymentAsync(
        string paymentRequestId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var secretApiKey = options.Value.SecretApiKey;

        if (string.IsNullOrWhiteSpace(secretApiKey) ||
            !secretApiKey.StartsWith(
                "xnd_development_",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Xendit sandbox simulation requires a development API key.");
        }

        if (string.IsNullOrWhiteSpace(paymentRequestId))
        {
            throw new InvalidOperationException(
                "Xendit payment request id is required for sandbox simulation.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v3/payment_requests/{Uri.EscapeDataString(paymentRequestId)}/simulate");

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{secretApiKey}:")));
        request.Headers.Add("api-version", ApiVersion);
        request.Content = JsonContent.Create(
            new SimulatePaymentRequest(amount));

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit gagal memproses simulasi pembayaran sandbox.");
        }

        var body = await response.Content
            .ReadFromJsonAsync<SimulatePaymentResponse>(
                cancellationToken: cancellationToken);

        if (body is null ||
            !string.Equals(
                body.Status,
                "PENDING",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit tidak mengonfirmasi simulasi pembayaran sandbox.");
        }
    }

    private sealed record SimulatePaymentRequest(
        [property: JsonPropertyName("amount")] decimal Amount);

    private sealed record SimulatePaymentResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("message")] string? Message);
}
