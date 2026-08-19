using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace NeverfadePos.Api.Payments.Xendit;

public sealed class XenditPaymentProvider(
    HttpClient httpClient,
    IOptions<XenditOptions> options)
    : IXenditPaymentProvider
{
    private const string ApiVersion = "2024-11-11";

    public async Task<XenditPaymentRequestResult> CreateQrisAsync(
        string referenceId,
        decimal amount,
        string description,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var secretApiKey = options.Value.SecretApiKey;

        if (string.IsNullOrWhiteSpace(secretApiKey))
        {
            throw new InvalidOperationException(
                "Xendit:SecretApiKey is required for payment creation.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v3/payment_requests");

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{secretApiKey}:")));
        request.Headers.Add("api-version", ApiVersion);
        request.Content = JsonContent.Create(new CreatePaymentRequest(
            referenceId,
            "PAY",
            "ID",
            "IDR",
            amount,
            "QRIS",
            description,
            new ChannelPropertiesRequest(expiresAt)));

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit gagal membuat QRIS payment request.");
        }

        var body = await response.Content
            .ReadFromJsonAsync<PaymentRequestResponse>(
                cancellationToken: cancellationToken)
            ?? throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit mengembalikan response payment kosong.");

        if (string.IsNullOrWhiteSpace(body.PaymentRequestId) ||
            string.IsNullOrWhiteSpace(body.ReferenceId))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit payment response tidak memiliki identifier wajib.");
        }

        var qrAction = body.Actions.FirstOrDefault(x =>
            string.Equals(
                x.Type,
                "PRESENT_TO_CUSTOMER",
                StringComparison.Ordinal) &&
            string.Equals(
                x.Descriptor,
                "QR_STRING",
                StringComparison.Ordinal));

        return new XenditPaymentRequestResult(
            body.PaymentRequestId,
            body.ReferenceId,
            body.RequestAmount,
            body.Status,
            qrAction?.Value,
            body.ChannelProperties?.ExpiresAt);
    }

    public async Task CancelPaymentRequestAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default)
    {
        var secretApiKey = options.Value.SecretApiKey;
        if (string.IsNullOrWhiteSpace(secretApiKey))
        {
            throw new InvalidOperationException(
                "Xendit:SecretApiKey is required for payment cancellation.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v3/payment_requests/{Uri.EscapeDataString(paymentRequestId)}/cancel");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{secretApiKey}:")));
        request.Headers.Add("api-version", ApiVersion);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum dapat membatalkan payment request.");
        }

        var body = await response.Content.ReadFromJsonAsync<PaymentRequestResponse>(
            cancellationToken: cancellationToken);
        if (body is null || !string.Equals(body.Status, "CANCELED", StringComparison.Ordinal))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum mengonfirmasi pembatalan payment request.");
        }
    }

    private sealed record CreatePaymentRequest(
        [property: JsonPropertyName("reference_id")] string ReferenceId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("request_amount")] decimal RequestAmount,
        [property: JsonPropertyName("channel_code")] string ChannelCode,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("channel_properties")] ChannelPropertiesRequest ChannelProperties);

    private sealed record ChannelPropertiesRequest(
        [property: JsonPropertyName("expires_at")] DateTime ExpiresAt);

    private sealed class PaymentRequestResponse
    {
        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("payment_request_id")]
        public string PaymentRequestId { get; set; } = string.Empty;

        [JsonPropertyName("request_amount")]
        public decimal RequestAmount { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("actions")]
        public List<PaymentAction> Actions { get; set; } = new();

        [JsonPropertyName("channel_properties")]
        public ChannelProperties? ChannelProperties { get; set; }
    }

    private sealed class PaymentAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("descriptor")]
        public string Descriptor { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ChannelProperties
    {
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }
}

public sealed class XenditProviderException(
    int providerStatusCode,
    string message)
    : Exception(message)
{
    public int ProviderStatusCode { get; } = providerStatusCode;
}
