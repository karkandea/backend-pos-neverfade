namespace NeverfadePos.Api.Payments.Xendit;

public interface IXenditPaymentProvider
{
    Task<XenditPaymentRequestResult> CreateQrisAsync(
        string referenceId,
        decimal amount,
        string description,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);

    Task<string> GetPaymentRequestStatusAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult("UNKNOWN");

    Task CancelPaymentRequestAsync(
        string paymentRequestId,
        CancellationToken cancellationToken = default);
}

public sealed record XenditPaymentRequestResult(
    string PaymentRequestId,
    string ReferenceId,
    decimal RequestAmount,
    string Status,
    string? QrString,
    DateTime? ExpiresAt);
