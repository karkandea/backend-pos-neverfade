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

    Task<XenditHostedSessionResult> CreateHostedSessionAsync(
        string referenceId,
        decimal amount,
        string description,
        DateTime expiresAt,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<XenditHostedSessionResult> GetHostedSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<XenditHostedSessionResult> CancelHostedSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<XenditPaymentResult> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

public sealed record XenditPaymentRequestResult(
    string PaymentRequestId,
    string ReferenceId,
    decimal RequestAmount,
    string Status,
    string? QrString,
    DateTime? ExpiresAt);

public sealed record XenditHostedSessionResult(
    string SessionId,
    string ReferenceId,
    decimal Amount,
    string Status,
    string? PaymentLinkUrl,
    DateTime? ExpiresAt,
    string? PaymentRequestId,
    string? PaymentId);

public sealed record XenditPaymentResult(
    string PaymentId,
    string ReferenceId,
    string PaymentRequestId,
    decimal RequestAmount,
    string Status,
    string ChannelCode,
    string Currency);
