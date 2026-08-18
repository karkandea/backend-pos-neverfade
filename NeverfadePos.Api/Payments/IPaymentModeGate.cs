using NeverfadePos.Api.DTOs.Payment;

namespace NeverfadePos.Api.Payments;

public interface IPaymentModeGate
{
    PaymentCapabilitiesDto GetCapabilities(Guid tenantId);

    void EnsureQrisAllowed(Guid tenantId);
}
