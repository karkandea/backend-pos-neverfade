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
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v3/payment_requests");
        ApplyAuth(request, includeApiVersion: true);
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

    public async Task<XenditHostedSessionResult> CreateHostedSessionAsync(
        string referenceId,
        decimal amount,
        string description,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var returnUrl = options.Value.CheckoutReturnUrl?.Trim();
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsedReturnUrl) ||
            parsedReturnUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Xendit:CheckoutReturnUrl must be an absolute HTTPS URL.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "sessions");
        ApplyAuth(request, includeApiVersion: false);
        request.Content = JsonContent.Create(new CreateHostedSessionRequest(
            referenceId,
            "PAY",
            "PAYMENT_LINK",
            amount,
            "IDR",
            "ID",
            "DISABLED",
            "id",
            description,
            expiresAt,
            returnUrl!,
            returnUrl!));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit gagal membuat hosted checkout session.");
        }

        var body = await ReadHostedSessionAsync(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(body.PaymentLinkUrl))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit hosted checkout tidak memiliki payment link.");
        }

        return MapHostedSession(body);
    }

    public async Task<XenditHostedSessionResult> GetHostedSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"sessions/{Uri.EscapeDataString(sessionId)}");
        ApplyAuth(request, includeApiVersion: false);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum dapat membaca hosted checkout session.");
        }

        return MapHostedSession(
            await ReadHostedSessionAsync(response, cancellationToken));
    }

    public async Task<XenditHostedSessionResult> CancelHostedSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"sessions/{Uri.EscapeDataString(sessionId)}/cancel");
        ApplyAuth(request, includeApiVersion: false);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum dapat membatalkan hosted checkout session.");
        }

        var result = MapHostedSession(
            await ReadHostedSessionAsync(response, cancellationToken));
        if (!string.Equals(result.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum mengonfirmasi pembatalan hosted checkout session.");
        }

        return result;
    }

    public async Task<XenditPaymentResult> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v3/payments/{Uri.EscapeDataString(paymentId)}");
        ApplyAuth(request, includeApiVersion: true);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum dapat membaca payment hosted checkout.");
        }

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(
            cancellationToken: cancellationToken)
            ?? throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit mengembalikan detail payment kosong.");

        return new XenditPaymentResult(
            body.PaymentId,
            body.ReferenceId,
            body.PaymentRequestId,
            body.RequestAmount,
            body.Status,
            body.ChannelCode,
            body.Currency);
    }

    public async Task<XenditPaymentRequestStatusResult> GetPaymentRequestAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v3/payment_requests/{Uri.EscapeDataString(paymentRequestId)}");
        ApplyAuth(request, includeApiVersion: true);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit belum dapat membaca status payment request.");
        }

        var body = await response.Content.ReadFromJsonAsync<PaymentRequestResponse>(
            cancellationToken: cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.Status))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit mengembalikan status payment request kosong.");
        }

        return new XenditPaymentRequestStatusResult(
            body.PaymentRequestId,
            body.ReferenceId,
            body.RequestAmount,
            body.Currency,
            body.ChannelCode,
            body.Status,
            body.FailureCode,
            body.LatestPaymentId,
            body.ChannelProperties?.ExpiresAt);
    }

    public async Task<string> GetPaymentRequestStatusAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default) =>
        (await GetPaymentRequestAsync(paymentRequestId, cancellationToken)).Status;

    public async Task CancelPaymentRequestAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v3/payment_requests/{Uri.EscapeDataString(paymentRequestId)}/cancel");
        ApplyAuth(request, includeApiVersion: true);

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

    private void ApplyAuth(HttpRequestMessage request, bool includeApiVersion)
    {
        var secretApiKey = options.Value.SecretApiKey;
        if (string.IsNullOrWhiteSpace(secretApiKey))
        {
            throw new InvalidOperationException(
                "Xendit:SecretApiKey is required for payment operations.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{secretApiKey}:")));
        if (includeApiVersion)
        {
            request.Headers.Add("api-version", ApiVersion);
        }
    }

    private static async Task<HostedSessionResponse> ReadHostedSessionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<HostedSessionResponse>(
            cancellationToken: cancellationToken)
            ?? throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit mengembalikan hosted checkout session kosong.");

        if (string.IsNullOrWhiteSpace(body.PaymentSessionId) ||
            string.IsNullOrWhiteSpace(body.ReferenceId))
        {
            throw new XenditProviderException(
                (int)response.StatusCode,
                "Xendit hosted checkout response tidak memiliki identifier wajib.");
        }

        return body;
    }

    private static XenditHostedSessionResult MapHostedSession(
        HostedSessionResponse body) => new(
            body.PaymentSessionId,
            body.ReferenceId,
            body.Amount,
            body.Status,
            body.PaymentLinkUrl,
            body.ExpiresAt,
            body.PaymentRequestId,
            body.PaymentId);

    private sealed record CreatePaymentRequest(
        [property: JsonPropertyName("reference_id")] string ReferenceId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("request_amount")] decimal RequestAmount,
        [property: JsonPropertyName("channel_code")] string ChannelCode,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("channel_properties")] ChannelPropertiesRequest ChannelProperties);

    private sealed record CreateHostedSessionRequest(
        [property: JsonPropertyName("reference_id")] string ReferenceId,
        [property: JsonPropertyName("session_type")] string SessionType,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("allow_save_payment_method")] string AllowSavePaymentMethod,
        [property: JsonPropertyName("locale")] string Locale,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("expires_at")] DateTime ExpiresAt,
        [property: JsonPropertyName("success_return_url")] string SuccessReturnUrl,
        [property: JsonPropertyName("cancel_return_url")] string CancelReturnUrl);

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

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("channel_code")]
        public string ChannelCode { get; set; } = string.Empty;

        [JsonPropertyName("latest_payment_id")]
        public string? LatestPaymentId { get; set; }

        [JsonPropertyName("failure_code")]
        public string? FailureCode { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("actions")]
        public List<PaymentAction> Actions { get; set; } = new();

        [JsonPropertyName("channel_properties")]
        public ChannelProperties? ChannelProperties { get; set; }
    }

    private sealed class HostedSessionResponse
    {
        [JsonPropertyName("payment_session_id")]
        public string PaymentSessionId { get; set; } = string.Empty;

        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("payment_link_url")]
        public string? PaymentLinkUrl { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("payment_request_id")]
        public string? PaymentRequestId { get; set; }

        [JsonPropertyName("payment_id")]
        public string? PaymentId { get; set; }
    }

    private sealed class PaymentResponse
    {
        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; } = string.Empty;

        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; set; } = string.Empty;

        [JsonPropertyName("payment_request_id")]
        public string PaymentRequestId { get; set; } = string.Empty;

        [JsonPropertyName("request_amount")]
        public decimal RequestAmount { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("channel_code")]
        public string ChannelCode { get; set; } = string.Empty;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;
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
