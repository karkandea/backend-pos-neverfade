using NeverfadePos.Api.DTOs.Payment;

namespace NeverfadePos.Api.Services.Payment;

public interface ISandboxQrisQaService
{
    Task<PaymentStatusDto> SimulateScannedQrisAsync(
        string qrString,
        CancellationToken cancellationToken = default);
}
