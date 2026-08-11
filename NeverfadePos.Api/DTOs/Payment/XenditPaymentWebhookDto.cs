using System.Text.Json.Serialization;

namespace NeverfadePos.Api.DTOs.Payment;

public sealed class XenditPaymentWebhookDto
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("business_id")]
    public string BusinessId { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("data")]
    public XenditPaymentWebhookDataDto Data { get; set; } = new();
}

public sealed class XenditPaymentWebhookDataDto
{
    [JsonPropertyName("payment_id")]
    public string PaymentId { get; set; } = string.Empty;

    [JsonPropertyName("payment_request_id")]
    public string PaymentRequestId { get; set; } = string.Empty;

    [JsonPropertyName("reference_id")]
    public string ReferenceId { get; set; } = string.Empty;

    [JsonPropertyName("request_amount")]
    public decimal RequestAmount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("channel_code")]
    public string ChannelCode { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("failure_code")]
    public string? FailureCode { get; set; }
}
