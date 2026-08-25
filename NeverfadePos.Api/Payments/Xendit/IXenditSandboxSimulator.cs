namespace NeverfadePos.Api.Payments.Xendit;

public interface IXenditSandboxSimulator
{
    Task SimulatePaymentAsync(
        string paymentRequestId,
        decimal amount,
        CancellationToken cancellationToken = default);
}
