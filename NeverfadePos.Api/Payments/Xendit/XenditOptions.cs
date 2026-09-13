namespace NeverfadePos.Api.Payments.Xendit;

public sealed class XenditOptions
{
    public string SecretApiKey { get; set; } = string.Empty;

    public string WebhookCallbackToken { get; set; } = string.Empty;

    public int QrisExpiryMinutes { get; set; } = 10;

    public int CheckoutExpiryMinutes { get; set; } = 30;

    public string CheckoutReturnUrl { get; set; } = string.Empty;
}
