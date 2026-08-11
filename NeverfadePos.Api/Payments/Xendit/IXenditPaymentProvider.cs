namespace NeverfadePos.Api.Payments.Xendit;

public interface IXenditPaymentProvider
{
    Task<XenditPaymentRequestResult> CreateQrisAsync(
        string referenceId,
        decimal amount,
        string description,
        CancellationToken cancellationToken = default);
}

public sealed record XenditPaymentRequestResult(
    string PaymentRequestId,
    string ReferenceId,
    decimal RequestAmount,
    string Status,
    string? QrString,
    DateTime? ExpiresAt);
