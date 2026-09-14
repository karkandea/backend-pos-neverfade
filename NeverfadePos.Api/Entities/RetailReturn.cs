using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public static class RetailReturnTypes
{
    public const string Return = "return";
    public const string Exchange = "exchange";

    public static bool IsValid(string? value) =>
        value is Return or Exchange;
}

public sealed class RetailReturn : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid TransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string Type { get; set; } = RetailReturnTypes.Return;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
    public Transaction? Transaction { get; set; }
    public ICollection<RetailReturnItem> Items { get; set; } = new List<RetailReturnItem>();
}
