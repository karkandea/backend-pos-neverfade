namespace NeverfadePos.Api.DTOs.Finance;

public sealed class FinanceSummaryDto
{
    public decimal AvailableBalance { get; set; }

    public decimal TotalSuccessfulNonCashIncome { get; set; }

    public decimal TotalWithdrawn { get; set; }

    public decimal PendingWithdrawalAmount { get; set; }
}
