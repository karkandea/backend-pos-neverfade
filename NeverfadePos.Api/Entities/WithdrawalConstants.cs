namespace NeverfadePos.Api.Entities;

public static class WithdrawalConstants
{
    public const string StatusRequested = "requested";
    public const string StatusProcessing = "processing";
    public const string StatusPaid = "paid";
    public const string StatusRejected = "rejected";
    public const string StatusCancelled = "cancelled";

    public const string BankPending = "pending";
    public const string BankVerified = "verified";
    public const string BankRejected = "rejected";

    public const decimal MinimumAmount = 100_000m;
}
