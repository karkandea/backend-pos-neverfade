using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;

namespace NeverfadePos.Api.Services.Payment;

public interface IPaymentService
{
    Task<QrisPaymentDto> CreateQrisAsync(
        CreateTransactionDto request,
        CancellationToken cancellationToken = default);

    Task<PaymentStatusDto> GetStatusAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task ProcessXenditWebhookAsync(
        string? callbackToken,
        XenditPaymentWebhookDto webhook,
        CancellationToken cancellationToken = default);
}
