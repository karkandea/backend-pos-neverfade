using System.ComponentModel.DataAnnotations;
using NeverfadePos.Api.DTOs.Transaction;

namespace NeverfadePos.Api.DTOs.Sales;

public sealed class CommitCashSaleRequestDto
{
    public Guid QuoteId { get; set; }
    public Guid QuoteVersion { get; set; }
    [Range(0, double.MaxValue)]
    public decimal AmountReceived { get; set; }
}

public sealed class CashSaleResponseDto
{
    public TransactionDto Data { get; set; } = new();
    public CashSaleMetaDto Meta { get; set; } = new();
}

public sealed class CashSaleMetaDto
{
    public string CorrelationId { get; set; } = string.Empty;
    public bool Replayed { get; set; }
    public Guid QuoteId { get; set; }
    public Guid QuoteVersion { get; set; }
}
