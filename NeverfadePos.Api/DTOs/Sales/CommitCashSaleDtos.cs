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

/// <summary>A durable server-side cash attempt; this is not a paid transaction.</summary>
public sealed class PreparedCashSaleDto
{
    public Guid QuoteId { get; set; }
    public Guid QuoteVersion { get; set; }
    public Guid OutletId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal AmountReceived { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "prepared";
    public Guid? TransactionId { get; set; }
    public DateTime PreparedAt { get; set; }
    public DateTime QuoteExpiresAt { get; set; }
}
