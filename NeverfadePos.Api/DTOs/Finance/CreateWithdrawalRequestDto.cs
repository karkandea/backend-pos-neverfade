using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Finance;

public sealed class CreateWithdrawalRequestDto
{
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}
